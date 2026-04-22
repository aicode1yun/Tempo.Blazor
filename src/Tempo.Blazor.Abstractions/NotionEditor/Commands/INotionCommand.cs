namespace Tempo.Blazor.NotionEditor.Commands;

/// <summary>
/// An undoable / redoable async mutation of the Notion editor state.
/// Each implementation must be self-contained: it holds all state needed
/// to both apply and reverse the operation, including provider references.
/// </summary>
public interface INotionCommand
{
    /// <summary>Human-readable label shown in undo / redo tooltips.</summary>
    string Description { get; }

    /// <summary>
    /// Applies the mutation to the provider and to the local in-memory block list.
    /// Must be idempotent when called after <see cref="UndoAsync"/> (i.e. safe for redo).
    /// </summary>
    Task ExecuteAsync();

    /// <summary>
    /// Reverts the mutation previously applied by <see cref="ExecuteAsync"/>.
    /// Must be idempotent when called after <see cref="ExecuteAsync"/>.
    /// </summary>
    Task UndoAsync();
}
