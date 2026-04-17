namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Defines which side of a node a port sits on.</summary>
public enum PortSide
{
    Top,
    Right,
    Bottom,
    Left
}

/// <summary>A connection port on a diagram node.</summary>
public sealed class DiagramPort
{
    /// <summary>Unique identifier (short Guid, e.g. "a3f8c21b").</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Human-readable name of the port.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Which side of the node the port sits on.</summary>
    public PortSide Side { get; set; } = PortSide.Top;

    /// <summary>Offset along the side as a ratio from 0.0 to 1.0.</summary>
    public double Offset { get; set; } = 0.5;

    /// <summary>Whether the port accepts incoming edges.</summary>
    public bool IsInput { get; set; } = true;

    /// <summary>Whether the port allows outgoing edges.</summary>
    public bool IsOutput { get; set; } = true;

    /// <summary>Magnet strategy for edge snapping. Supported: cardinal, perimeter, custom.</summary>
    public string? MagnetStrategy { get; set; }
}
