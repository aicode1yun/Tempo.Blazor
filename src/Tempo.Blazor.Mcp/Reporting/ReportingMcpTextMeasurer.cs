using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>Small deterministic text measurer used by MCP preview rendering.</summary>
public sealed class ReportingMcpTextMeasurer : ITextMeasurer
{
    /// <inheritdoc />
    public TextMeasurement MeasureRun(TextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Text ?? string.Empty;
        var glyphs = text.EnumerateRunes().Count();
        var fontSize = request.FontSize <= 0 ? 10 : request.FontSize;
        var weightFactor = request.Bold ? 0.58 : 0.54;
        var width = glyphs * fontSize * weightFactor;
        if (glyphs > 1)
        {
            width += (glyphs - 1) * request.LetterSpacing;
        }

        var ascent = fontSize * 0.78;
        var descent = fontSize * 0.22;
        var lineGap = fontSize * 0.2;
        return new TextMeasurement(
            Math.Max(0, width),
            ascent,
            descent,
            lineGap,
            ascent + descent + lineGap,
            glyphs,
            0,
            0);
    }
}
