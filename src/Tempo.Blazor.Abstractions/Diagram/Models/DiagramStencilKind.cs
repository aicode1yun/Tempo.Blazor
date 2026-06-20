namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Identifies what kind of diagram element a stencil creates.</summary>
public enum DiagramStencilKind
{
    /// <summary>The stencil creates a diagram node.</summary>
    Node = 0,

    /// <summary>The stencil creates a diagram edge or relationship.</summary>
    Edge = 1
}
