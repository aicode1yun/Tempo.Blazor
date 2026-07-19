using System.Text.Json;
using FluentAssertions;
using SkiaSharp;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Phase 1 of the headless document runtime: text measurement parity. The extractor reads glyph
/// advance widths and vertical metrics from the SAME ReportPdfFontFace bytes the PDF renderer
/// embeds (via SKTypeface/SKFont with linear, unhinted metrics in font units) and serializes them
/// into a compact JSON table the JS layout bundle measures with — measurement and drawing parity
/// by construction, no .NET↔JS callbacks per glyph.
/// </summary>
public class TempoFontAdvanceTableExtractorTests
{
    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static ReportPdfFontFace CreateFace(int weight = 400, string style = "normal")
        => new("Dancing Script", weight, style, File.ReadAllBytes(FontPath));

    // ── Extraction ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractFace_ReadsVerticalMetricsInFontUnits()
    {
        var face = new TempoFontAdvanceTableExtractor().ExtractFace(CreateFace());

        face.Family.Should().Be("Dancing Script");
        face.Weight.Should().Be(400);
        face.Style.Should().Be("normal");
        face.UnitsPerEm.Should().BeGreaterThan(0);
        face.Ascent.Should().BeGreaterThan(0, "ascent is stored as a positive distance in font units");
        face.Descent.Should().BeGreaterThan(0, "descent is stored as a positive distance in font units");
        face.LineGap.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ExtractFace_CoversLatinAndCzechDiacritics()
    {
        var face = new TempoFontAdvanceTableExtractor().ExtractFace(CreateFace());

        foreach (var ch in "AWij09 ,.PŘÍLIŠžluťoučkýkůňúpělďábelskéódy")
        {
            face.Advances.Should().ContainKey(ch, $"'{ch}' (U+{(int)ch:X4}) must have an advance width");
            face.Advances[ch].Should().BeGreaterThan(0f, $"'{ch}' must have a positive advance");
        }

        // Proportional font: wide and narrow glyphs must differ — a fixed-width table would
        // silently destroy line-breaking parity.
        face.Advances['W'].Should().BeGreaterThan(face.Advances['i']);
    }

    [Fact]
    public void ExtractFace_AdvancesMatchSkFontMeasureTextExactly()
    {
        var reportFace = CreateFace();
        var face = new TempoFontAdvanceTableExtractor().ExtractFace(reportFace);

        using var data = SKData.CreateCopy(reportFace.Bytes);
        using var typeface = SKTypeface.FromData(data);
        using var font = new SKFont(typeface!, face.UnitsPerEm)
        {
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true,
        };

        // Per-glyph advances are the parity contract: each extracted advance must equal
        // SKFont.MeasureText of that character exactly (zero deviation) — this is what both the
        // JS line breaker and the PDF glyph positioning consume.
        foreach (var rune in "Wave Příliš žluťoučký kůň 0123456789 y,W.i".EnumerateRunes())
        {
            var measured = font.MeasureText(char.ConvertFromUtf32(rune.Value));
            face.Advances[rune.Value].Should().Be(
                measured,
                $"advance for '{char.ConvertFromUtf32(rune.Value)}' must equal SKFont.MeasureText in font units");
        }

        // Whole strings: Skia's measureText accumulates in 32-bit floats with an internal order
        // that is not observable, so full-string sums agree only up to float accumulation noise.
        // Bound it hard: ≤ 0.001 font units per glyph (≈ 1.6e-5 px per glyph at 16px/1000upem).
        foreach (var sample in new[] { "Wave", "Příliš žluťoučký kůň", "0123456789", "y, W. i" })
        {
            var summed = 0d;
            foreach (var rune in sample.EnumerateRunes())
            {
                summed += face.Advances[rune.Value];
            }

            var measured = font.MeasureText(sample);
            summed.Should().BeApproximately(
                measured,
                0.001 * sample.Length,
                $"advance sum for \"{sample}\" must match SKFont.MeasureText up to float accumulation noise");
        }
    }

    [Fact]
    public void ExtractFace_UnknownGlyphsAreOmittedAndMissingAdvanceIsExposed()
    {
        var face = new TempoFontAdvanceTableExtractor().ExtractFace(CreateFace());

        // Dancing Script has no CJK coverage; the table must omit the glyph instead of storing 0
        // so the JS side can fall back with diagnostics.
        face.Advances.Should().NotContainKey('漢');
        face.MissingGlyphAdvance.Should().BeGreaterThanOrEqualTo(0f);
    }

    [Fact]
    public void ExtractFace_BoldAndItalicSystemFaces_KeepPerGlyphParityWithSkia()
    {
        // Representative bold/italic faces from the host system (the committed Dancing Script
        // variable font ships a single instance). Skipped quietly on machines without them —
        // same pattern as the DejaVu-based ReportPdfRenderer tests.
        (string Path, int Weight, string Style)[] candidates =
        [
            (@"C:\Windows\Fonts\arial.ttf", 400, "normal"),
            (@"C:\Windows\Fonts\arialbd.ttf", 700, "normal"),
            (@"C:\Windows\Fonts\ariali.ttf", 400, "italic"),
            ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 400, "normal"),
            ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 700, "normal"),
        ];

        foreach (var (path, weight, style) in candidates.Where(candidate => File.Exists(candidate.Path)))
        {
            var reportFace = new ReportPdfFontFace("System Sample", weight, style, File.ReadAllBytes(path));
            var face = new TempoFontAdvanceTableExtractor().ExtractFace(reportFace);

            using var data = SKData.CreateCopy(reportFace.Bytes);
            using var typeface = SKTypeface.FromData(data);
            using var font = new SKFont(typeface!, face.UnitsPerEm)
            {
                Hinting = SKFontHinting.None,
                LinearMetrics = true,
                Subpixel = true,
            };

            foreach (var rune in "Příliš žluťoučký kůň Wij09,.".EnumerateRunes())
            {
                if (!face.Advances.TryGetValue(rune.Value, out var advance))
                {
                    continue;
                }

                advance.Should().Be(
                    font.MeasureText(char.ConvertFromUtf32(rune.Value)),
                    $"advance parity must hold for '{char.ConvertFromUtf32(rune.Value)}' in {Path.GetFileName(path)} ({weight} {style})");
            }
        }
    }

    // ── Thread-safe lazy cache ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractFace_CachesPerFontFace_SameInstanceForEqualBytesAndKey()
    {
        var extractor = new TempoFontAdvanceTableExtractor();

        var first = extractor.ExtractFace(CreateFace());
        var second = extractor.ExtractFace(CreateFace());

        second.Should().BeSameAs(first, "extraction is cached per (family, weight, style, bytes)");
        extractor.ExtractFace(CreateFace(weight: 700)).Should().NotBeSameAs(first, "a different weight is a different face");
    }

    [Fact]
    public async Task ExtractFace_IsThreadSafe_ParallelCallersGetTheSameCachedInstance()
    {
        var extractor = new TempoFontAdvanceTableExtractor();
        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => extractor.ExtractFace(CreateFace()))));

        results.Distinct().Should().HaveCount(1, "parallel extraction must produce one cached face");
    }

    // ── Compact JSON for the JS side ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildAdvanceTablesJson_SerializesFacesWithCodePointKeyedAdvances()
    {
        var json = new TempoFontAdvanceTableExtractor().BuildAdvanceTablesJson(
            [CreateFace(), CreateFace(weight: 700)]);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);

        var faces = root.GetProperty("faces");
        faces.GetArrayLength().Should().Be(2);

        var face = faces[0];
        face.GetProperty("family").GetString().Should().Be("Dancing Script");
        face.GetProperty("weight").GetInt32().Should().Be(400);
        face.GetProperty("style").GetString().Should().Be("normal");
        face.GetProperty("unitsPerEm").GetInt32().Should().BeGreaterThan(0);
        face.GetProperty("ascent").GetDouble().Should().BeGreaterThan(0);
        face.GetProperty("descent").GetDouble().Should().BeGreaterThan(0);

        var advances = face.GetProperty("advances");
        advances.TryGetProperty(((int)'A').ToString(), out var advanceA).Should().BeTrue("advances are keyed by decimal code point");
        advanceA.GetDouble().Should().BeGreaterThan(0);
        advances.TryGetProperty(((int)'ř').ToString(), out var advanceR).Should().BeTrue("Czech diacritics must be present");
        advanceR.GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildAdvanceTablesJson_IsDeterministic()
    {
        var extractor = new TempoFontAdvanceTableExtractor();
        var first = extractor.BuildAdvanceTablesJson([CreateFace()]);
        var second = new TempoFontAdvanceTableExtractor().BuildAdvanceTablesJson([CreateFace()]);

        second.Should().Be(first, "same bytes must serialize to the identical JSON");
    }
}
