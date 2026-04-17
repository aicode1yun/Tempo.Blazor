namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Defines a default port on a diagram stencil.</summary>
public sealed class DiagramStencilPortDef
{
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
