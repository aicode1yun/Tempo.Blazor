namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Defines a fixed connection point on a node's boundary or interior.</summary>
public sealed class DiagramConnectionConstraint
{
    /// <summary>Relative X position (0–1) along the node width. 0 = left edge, 1 = right edge.</summary>
    public double RelativeX { get; set; }

    /// <summary>Relative Y position (0–1) along the node height. 0 = top edge, 1 = bottom edge.</summary>
    public double RelativeY { get; set; }

    /// <summary>When true, the point is projected onto the node perimeter. When false, the point is inside the bounding box.</summary>
    public bool Perimeter { get; set; }

    /// <summary>Horizontal offset in pixels applied after relative positioning.</summary>
    public double Dx { get; set; }

    /// <summary>Vertical offset in pixels applied after relative positioning.</summary>
    public double Dy { get; set; }

    /// <summary>Creates a copy of this constraint.</summary>
    public DiagramConnectionConstraint Clone() => new()
    {
        RelativeX = RelativeX,
        RelativeY = RelativeY,
        Perimeter = Perimeter,
        Dx = Dx,
        Dy = Dy
    };
}
