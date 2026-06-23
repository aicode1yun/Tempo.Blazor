using System.Text;

namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Font-table based text measurer used by the reporting engine.</summary>
public sealed class TableTextMeasurer : ITextMeasurer
{
    private readonly FontMetricTable _metricTable;

    /// <summary>Initializes a table-backed text measurer.</summary>
    public TableTextMeasurer(FontMetricTable metricTable)
    {
        _metricTable = metricTable;
    }

    /// <inheritdoc />
    public TextMeasurement MeasureRun(TextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var primaryFace = _metricTable.ResolveFace(request.FontFamily, request.Bold, request.Italic);
        var styleKey = FontStyleKey.From(request.Bold, request.Italic);
        var width = 0d;
        var glyphCount = 0;
        var fallbackGlyphCount = 0;
        var missingGlyphCount = 0;
        var previousCodePoint = (int?)null;
        var previousFace = (FontMetricFace?)null;
        var hintedPixelsPerEm = GetHintedPixelsPerEm(request.FontSize);
        var maxAscent = Scale(primaryFace.Ascent, request.FontSize, primaryFace.UnitsPerEm);
        var maxDescent = Scale(Math.Abs(primaryFace.Descent), request.FontSize, primaryFace.UnitsPerEm);
        var maxLineGap = Scale(primaryFace.LineGap, request.FontSize, primaryFace.UnitsPerEm);

        foreach (var rune in (request.Text ?? string.Empty).EnumerateRunes())
        {
            var codePoint = rune.Value;
            var missingGlyph = false;
            var face = primaryFace.ContainsCodePoint(codePoint)
                ? primaryFace
                : _metricTable.FindFallbackFace(codePoint, styleKey);
            if (face is null)
            {
                face = primaryFace;
                missingGlyph = true;
                missingGlyphCount++;
            }
            else if (!ReferenceEquals(face, primaryFace))
            {
                fallbackGlyphCount++;
            }

            if (request.Kerning && previousCodePoint.HasValue && ReferenceEquals(previousFace, face))
            {
                width += Scale(face.GetKerning(previousCodePoint.Value, codePoint), request.FontSize, face.UnitsPerEm);
            }

            width += GetAdvanceWidth(face, codePoint, request.FontSize, hintedPixelsPerEm, missingGlyph);
            maxAscent = Math.Max(maxAscent, Scale(face.Ascent, request.FontSize, face.UnitsPerEm));
            maxDescent = Math.Max(maxDescent, Scale(Math.Abs(face.Descent), request.FontSize, face.UnitsPerEm));
            maxLineGap = Math.Max(maxLineGap, Scale(face.LineGap, request.FontSize, face.UnitsPerEm));
            previousCodePoint = codePoint;
            previousFace = face;
            glyphCount++;
        }

        if (glyphCount > 1 && Math.Abs(request.LetterSpacing) > double.Epsilon)
        {
            width += (glyphCount - 1) * request.LetterSpacing;
        }

        var lineHeight = maxAscent + maxDescent + maxLineGap;
        return new TextMeasurement(
            Width: Math.Max(0, width),
            Ascent: maxAscent,
            Descent: maxDescent,
            LineGap: maxLineGap,
            LineHeight: lineHeight,
            GlyphCount: glyphCount,
            FallbackGlyphCount: fallbackGlyphCount,
            MissingGlyphCount: missingGlyphCount);
    }

    private static double Scale(double fontUnits, double fontSize, int unitsPerEm)
        => fontUnits * fontSize / unitsPerEm;

    private static double GetAdvanceWidth(FontMetricFace face, int codePoint, double fontSize, int? hintedPixelsPerEm, bool missingGlyph)
    {
        if (hintedPixelsPerEm.HasValue)
        {
            if (!missingGlyph && face.TryGetHintedAdvanceWidth(codePoint, hintedPixelsPerEm.Value, out var hintedWidth))
            {
                return hintedWidth;
            }

            if (missingGlyph && face.TryGetHintedMissingGlyphAdvanceWidth(hintedPixelsPerEm.Value, out var missingHintedWidth))
            {
                return missingHintedWidth;
            }
        }

        return Scale(face.GetAdvanceWidth(codePoint), fontSize, face.UnitsPerEm);
    }

    private static int? GetHintedPixelsPerEm(double fontSize)
    {
        var rounded = Math.Round(fontSize);
        if (rounded < 1 || rounded > byte.MaxValue || Math.Abs(fontSize - rounded) > 0.0001)
        {
            return null;
        }

        return (int)rounded;
    }
}
