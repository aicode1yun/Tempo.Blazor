namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Declarative layout definition for a diagram stencil.</summary>
public sealed class DiagramStencilLayout
{
    /// <summary>Background shape. Supported: "rectangle", "rounded", "ellipse", "diamond", "document", "weak-entity".</summary>
    public string BackgroundShape { get; set; } = "rectangle";

    /// <summary>Fill color (CSS color value).</summary>
    public string? Fill { get; set; }

    /// <summary>Stroke color (CSS color value).</summary>
    public string? Stroke { get; set; }

    /// <summary>Stroke width in pixels.</summary>
    public double? StrokeWidth { get; set; }

    /// <summary>Layout sections rendered inside the stencil.</summary>
    public List<DiagramStencilSection> Sections { get; set; } = [];
}
