namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>
/// A predefined connection point on a stencil, similar to draw.io constraints.
/// When an edge connects to this point, it uses a fixed <see cref="DiagramConnectionConstraint"/>
/// instead of floating port snapping.
/// </summary>
public sealed class DiagramStencilConnectionPoint
{
    /// <summary>Human-readable name (e.g. "N", "NE", "Center").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Relative X position (0–1) within the stencil bounding box.
    /// 0 = left edge, 1 = right edge.
    /// </summary>
    public double RelativeX { get; set; }

    /// <summary>
    /// Relative Y position (0–1) within the stencil bounding box.
    /// 0 = top edge, 1 = bottom edge.
    /// </summary>
    public double RelativeY { get; set; }

    /// <summary>
    /// When true, the point is projected onto the node perimeter based on its background shape.
    /// When false, the point is kept at the exact relative coordinate inside the bounding box.
    /// </summary>
    public bool Perimeter { get; set; } = true;
}
