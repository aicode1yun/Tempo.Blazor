namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A single 2D point on the diagram canvas.</summary>
public sealed class DiagramPoint
{
    /// <summary>X coordinate in pixels.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate in pixels.</summary>
    public double Y { get; set; }

    public DiagramPoint() { }

    public DiagramPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}
