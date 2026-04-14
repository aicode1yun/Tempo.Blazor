namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Tracks undoable/redoable commands for a diagram editor instance.</summary>
public interface IDiagramCommandStack
{
    /// <summary>Raised whenever the stack changes (push, undo, redo, clear).</summary>
    event Action? OnStackChanged;

    /// <summary>Whether at least one command can be undone.</summary>
    bool CanUndo { get; }

    /// <summary>Whether at least one command can be redone.</summary>
    bool CanRedo { get; }

    /// <summary>Name of the next command that would be undone.</summary>
    string? NextUndoName { get; }

    /// <summary>Name of the next command that would be redone.</summary>
    string? NextRedoName { get; }

    /// <summary>Pushes a new command onto the stack and executes it.</summary>
    void Push(IDiagramCommand command);

    /// <summary>Undoes the most recent command.</summary>
    void Undo();

    /// <summary>Redoes the most recently undone command.</summary>
    void Redo();

    /// <summary>Clears the entire stack (useful on document switch).</summary>
    void Clear();
}
