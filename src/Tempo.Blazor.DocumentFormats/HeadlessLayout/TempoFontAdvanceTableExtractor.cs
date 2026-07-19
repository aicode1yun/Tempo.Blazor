using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>
/// Precomputed glyph metrics for one font face, in font units (unhinted, linear). Extracted from
/// the same TTF/OTF bytes the PDF renderer embeds, so headless layout measures with the exact
/// advances the drawn glyphs will have.
/// </summary>
public sealed record TempoFontAdvanceFace
{
    /// <summary>Creates a face metric record.</summary>
    public TempoFontAdvanceFace(
        string family,
        int weight,
        string style,
        int unitsPerEm,
        double ascent,
        double descent,
        double lineGap,
        double missingGlyphAdvance,
        IReadOnlyDictionary<int, double> advances)
    {
        Family = family;
        Weight = weight;
        Style = style;
        UnitsPerEm = unitsPerEm;
        Ascent = ascent;
        Descent = descent;
        LineGap = lineGap;
        MissingGlyphAdvance = missingGlyphAdvance;
        Advances = advances;
    }

    /// <summary>Font family name as referenced by document text styles.</summary>
    public string Family { get; }

    /// <summary>CSS-like numeric weight (400, 700, …).</summary>
    public int Weight { get; }

    /// <summary>CSS-like style, normal or italic.</summary>
    public string Style { get; }

    /// <summary>Font design units per em from the font's head table.</summary>
    public int UnitsPerEm { get; }

    /// <summary>Ascender as a positive distance in font units.</summary>
    public double Ascent { get; }

    /// <summary>Descender as a positive distance in font units.</summary>
    public double Descent { get; }

    /// <summary>Line gap (leading) in font units.</summary>
    public double LineGap { get; }

    /// <summary>Advance of the missing-glyph (.notdef) slot in font units.</summary>
    public double MissingGlyphAdvance { get; }

    /// <summary>Advance widths in font units keyed by Unicode code point. Code points the font has no glyph for are omitted.</summary>
    public IReadOnlyDictionary<int, double> Advances { get; }
}

/// <summary>
/// Extracts glyph advance tables and vertical metrics from <see cref="ReportPdfFontFace"/> bytes
/// via SkiaSharp (SKTypeface/SKFont with unhinted linear metrics at font-unit scale) and
/// serializes them into the compact JSON the headless JS layout bundle measures with. Extraction
/// is cached per face (family, weight, style, content hash) behind a thread-safe lazy cache —
/// repeated layout requests never re-parse font bytes.
/// </summary>
public sealed class TempoFontAdvanceTableExtractor
{
    /// <summary>Version of the JSON contract consumed by the JS measurer.</summary>
    public const int SchemaVersion = 1;

    // Default coverage: Basic Latin, Latin-1 Supplement, Latin Extended-A/B (Czech, Slovak,
    // Polish, Hungarian diacritics), general punctuation (quotes, dashes, ellipsis) and the
    // euro sign. Code points the font cannot map are omitted from the table.
    private static readonly (int First, int Last)[] DefaultCodePointRanges =
    [
        (0x0020, 0x007E),
        (0x00A0, 0x024F),
        (0x2010, 0x2027),
        (0x2030, 0x203A),
        (0x20AC, 0x20AC),
    ];

    private readonly ConcurrentDictionary<string, Lazy<TempoFontAdvanceFace>> _cache = new();
    private readonly int[] _additionalCodePoints;

    /// <summary>Shared process-wide extractor instance.</summary>
    public static TempoFontAdvanceTableExtractor Shared { get; } = new();

    /// <summary>Creates an extractor, optionally extending the default Latin coverage.</summary>
    public TempoFontAdvanceTableExtractor(IEnumerable<int>? additionalCodePoints = null)
        => _additionalCodePoints = additionalCodePoints?.Distinct().Order().ToArray() ?? [];

    /// <summary>Extracts (or returns the cached) advance table for one font face.</summary>
    public TempoFontAdvanceFace ExtractFace(ReportPdfFontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        var key = CacheKey(face);
        var lazy = _cache.GetOrAdd(key, _ => new Lazy<TempoFontAdvanceFace>(
            () => ExtractFaceCore(face),
            LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    /// <summary>Builds the compact JSON advance-table document for a set of font faces.</summary>
    public string BuildAdvanceTablesJson(IEnumerable<ReportPdfFontFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartArray("faces");
            foreach (var face in faces)
            {
                WriteFace(writer, ExtractFace(face));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteFace(Utf8JsonWriter writer, TempoFontAdvanceFace face)
    {
        writer.WriteStartObject();
        writer.WriteString("family", face.Family);
        writer.WriteNumber("weight", face.Weight);
        writer.WriteString("style", face.Style);
        writer.WriteNumber("unitsPerEm", face.UnitsPerEm);
        writer.WriteNumber("ascent", face.Ascent);
        writer.WriteNumber("descent", face.Descent);
        writer.WriteNumber("lineGap", face.LineGap);
        writer.WriteNumber("missingGlyphAdvance", face.MissingGlyphAdvance);
        writer.WriteStartObject("advances");
        foreach (var codePoint in face.Advances.Keys.Order())
        {
            writer.WriteNumber(codePoint.ToString(CultureInfo.InvariantCulture), face.Advances[codePoint]);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private TempoFontAdvanceFace ExtractFaceCore(ReportPdfFontFace face)
    {
        using var data = SKData.CreateCopy(face.Bytes);
        using var typeface = SKTypeface.FromData(data)
            ?? throw new InvalidOperationException(
                $"Font face '{face.Family}' ({face.Weight} {face.Style}) does not contain a parseable TTF/OTF font.");

        var unitsPerEm = typeface.UnitsPerEm > 0 ? typeface.UnitsPerEm : 1000;

        // Font-unit scale with unhinted linear metrics: advances come out in design units, the
        // same linear space Skia uses when it draws the embedded font into the PDF.
        using var font = new SKFont(typeface, unitsPerEm)
        {
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true,
        };

        font.GetFontMetrics(out var metrics);

        var codePoints = EnumerateCodePoints().ToArray();
        var glyphs = new ushort[codePoints.Length];
        for (var i = 0; i < codePoints.Length; i++)
        {
            glyphs[i] = (ushort)typeface.GetGlyph(codePoints[i]);
        }

        var widths = font.GetGlyphWidths(glyphs);

        var advances = new Dictionary<int, double>(codePoints.Length);
        for (var i = 0; i < codePoints.Length; i++)
        {
            if (glyphs[i] != 0)
            {
                advances[codePoints[i]] = widths[i];
            }
        }

        var missingWidths = font.GetGlyphWidths([(ushort)0]);

        return new TempoFontAdvanceFace(
            face.Family,
            face.Weight,
            NormalizeStyle(face.Style),
            unitsPerEm,
            -metrics.Ascent,
            metrics.Descent,
            metrics.Leading,
            missingWidths[0],
            advances);
    }

    private IEnumerable<int> EnumerateCodePoints()
    {
        foreach (var (first, last) in DefaultCodePointRanges)
        {
            for (var codePoint = first; codePoint <= last; codePoint++)
            {
                yield return codePoint;
            }
        }

        foreach (var codePoint in _additionalCodePoints)
        {
            yield return codePoint;
        }
    }

    private static string NormalizeStyle(string? style)
        => string.IsNullOrWhiteSpace(style) ? "normal" : style.Trim().ToLowerInvariant();

    private string CacheKey(ReportPdfFontFace face)
    {
        var hash = Convert.ToHexString(SHA256.HashData(face.Bytes));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{face.Family}|{face.Weight}|{NormalizeStyle(face.Style)}|{hash}");
    }
}
