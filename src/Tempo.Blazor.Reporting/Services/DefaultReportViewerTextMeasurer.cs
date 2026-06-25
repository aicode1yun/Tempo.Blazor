using System.Globalization;
using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Blazor.Reporting.Services;

/// <summary>Deterministic fallback text measurer used when a host does not register font metrics.</summary>
public sealed class DefaultReportViewerTextMeasurer : ITextMeasurer
{
    /// <inheritdoc />
    public TextMeasurement MeasureRun(TextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var glyphCount = 0;
        var width = 0d;
        foreach (var rune in request.Text.EnumerateRunes())
        {
            glyphCount++;
            width += IsCjk(rune.Value) ? request.FontSize : request.FontSize * 0.5;
        }

        width += Math.Max(0, glyphCount - 1) * request.LetterSpacing;
        return new TextMeasurement(
            Width: Math.Max(0, width),
            Ascent: request.FontSize * 0.8,
            Descent: request.FontSize * 0.2,
            LineGap: 0,
            LineHeight: request.FontSize,
            GlyphCount: glyphCount,
            FallbackGlyphCount: 0,
            MissingGlyphCount: 0);
    }

    private static bool IsCjk(int codePoint)
        => CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0) == UnicodeCategory.OtherLetter &&
           ((codePoint >= 0x3040 && codePoint <= 0x30ff) ||
            (codePoint >= 0x3400 && codePoint <= 0x9fff));
}
