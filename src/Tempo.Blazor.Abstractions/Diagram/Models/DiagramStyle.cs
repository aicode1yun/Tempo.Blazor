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
}
