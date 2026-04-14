namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>An undoable/redoable mutation of a <see cref="Tempo.Blazor.Components.Diagram.Models.DiagramDocument"/>.</summary>
public interface IDiagramCommand
{
    /// <summary>Human-readable name shown in undo/redo tooltips. (e.g. "Move nodes", "Add edge").</summary>
    string Name { get; }

    /// <summary>Applies the mutation to the document.</summary>
    void Execute();

    /// <summary>Reverts the mutation.</summary>
    void Undo();
}
