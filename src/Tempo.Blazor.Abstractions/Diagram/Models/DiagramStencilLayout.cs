namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Declarative layout definition for a diagram stencil.</summary>
public sealed class DiagramStencilLayout
{
    /// <summary>
    /// Background shape fallback. Supported: "rectangle", "rounded", "ellipse", "diamond", "document", "weak-entity",
    /// "cylinder", "cloud", "note", "actor", "lollipop", "component", "cube", "double-ellipse", "pentagon", "half-ellipse",
    /// "parallelogram", "triangle", "star", "hexagon", "sticky-note", "pool".
    /// </summary>
    public string BackgroundShape { get; set; } = "rectangle";

    /// <summary>
    /// Optional SVG content rendered as the stencil background.
    /// When provided, the component renders an SVG element with this content
    /// instead of using the CSS-based BackgroundShape fallback.
    /// The SVG should be designed for a 100x100 viewBox and will be stretched
    /// to fill the node unless <see cref="PreserveAspectRatio"/> is set.
    /// </summary>
    public string? ShapeSvg { get; set; }

    /// <summary>
    /// When <c>true</c>, the SVG background preserves its aspect ratio
    /// (uses <c>preserveAspectRatio="xMidYMid meet"</c>).
    /// When <c>false</c> (default), the SVG stretches to fill the node bounds.
    /// </summary>
    public bool PreserveAspectRatio { get; set; }

    /// <summary>Fill color (CSS color value).</summary>
    public string? Fill { get; set; }

    /// <summary>Stroke color (CSS color value).</summary>
    public string? Stroke { get; set; }

    /// <summary>Stroke width in pixels.</summary>
    public double? StrokeWidth { get; set; }

    /// <summary>Whether the stencil supports corner-radius editing in the properties panel.</summary>
    public bool SupportsCornerRadius { get; set; } = true;

    /// <summary>
    /// Content placement relative to the shape background.
    /// "overlay" (default) renders sections on top of the shape.
    /// "below" renders sections underneath the shape (e.g. for actor/person stick figures).
    /// </summary>
    public string ContentPosition { get; set; } = "overlay";

    /// <summary>Layout sections rendered inside the stencil.</summary>
    public List<DiagramStencilSection> Sections { get; set; } = [];
}
