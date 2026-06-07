using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Modeling;

namespace Tempo.Blazor.Components.Modeling;

/// <summary>Describes a modeling element dropped onto a diagram preview.</summary>
public sealed class ModelingNodeDroppedEventArgs
{
    /// <summary>Existing semantic model element reused by the new diagram node.</summary>
    public required ModelingElementDto Element { get; init; }

    /// <summary>Drop point in diagram document coordinates.</summary>
    public required DiagramPoint Point { get; init; }

    /// <summary>Identifier of the diagram node created for this dropped occurrence.</summary>
    public required string NodeId { get; init; }
}
