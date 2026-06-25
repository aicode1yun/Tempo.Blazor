using System.Globalization;
using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Reporting.Engine.Tests.Layout;

internal sealed class FixedTextMeasurer : ITextMeasurer
{
    public TextMeasurement MeasureRun(TextMeasureRequest request)
    {
        var width = 0d;
        foreach (var rune in request.Text.EnumerateRunes())
        {
            width += IsCjk(rune.Value) ? 10 : 5;
        }

        width += Math.Max(0, request.Text.EnumerateRunes().Count() - 1) * request.LetterSpacing;
        var lineHeight = request.FontSize;
        return new TextMeasurement(
            width,
            Ascent: lineHeight * 0.8,
            Descent: lineHeight * 0.2,
            LineGap: 0,
            LineHeight: lineHeight,
            GlyphCount: request.Text.EnumerateRunes().Count(),
            FallbackGlyphCount: 0,
            MissingGlyphCount: 0);
    }

    private static bool IsCjk(int codePoint)
        => CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0) == UnicodeCategory.OtherLetter &&
           ((codePoint >= 0x3040 && codePoint <= 0x30ff) ||
            (codePoint >= 0x3400 && codePoint <= 0x9fff));
}
