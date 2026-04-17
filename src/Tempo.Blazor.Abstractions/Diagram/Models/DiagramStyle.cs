namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Visual style overrides for a diagram element (node or edge).</summary>
public sealed class DiagramStyle
{
    /// <summary>Fill color (CSS color value).</summary>
    public string? Fill { get; set; }

    /// <summary>Stroke color (CSS color value).</summary>
    public string? Stroke { get; set; }

    /// <summary>Stroke width in pixels.</summary>
    public double? StrokeWidth { get; set; }

    /// <summary>CSS stroke dasharray pattern. (e.g. "5,5" for dashed).</summary>
    public string? StrokeDasharray { get; set; }

    /// <summary>Text color (CSS color value).</summary>
    public string? Color { get; set; }

    /// <summary>Font family.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size in pixels.</summary>
    public double? FontSize { get; set; }

    /// <summary>Opacity from 0.0 to 1.0.</summary>
    public double? Opacity { get; set; }

    /// <summary>Corner radius in pixels for rectangles.</summary>
    public double? Radius { get; set; }

    /// <summary>Horizontal text alignment. left, center, right.</summary>
    public string? TextAlign { get; set; }

    /// <summary>Vertical text alignment. top, middle, bottom.</summary>
    public string? VerticalAlign { get; set; }

    /// <summary>Whether text is bold.</summary>
    public bool? IsBold { get; set; }

    /// <summary>Whether text is italic.</summary>
    public bool? IsItalic { get; set; }

    /// <summary>Whether text is underlined.</summary>
    public bool? IsUnderline { get; set; }

    /// <summary>Whether the shape has a shadow effect.</summary>
    public bool? HasShadow { get; set; }

    /// <summary>Stroke dash pattern. e.g. solid, dashed, dotted, dash-dot.</summary>
    public string? StrokeDashPattern { get; set; }

    /// <summary>Whether MathJax rendering is enabled for this element's text.</summary>
    public bool? EnableMathJax { get; set; }
}
