namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Undoable and redoable document editor mutation.</summary>
public interface IDocumentEditorCommand
{
    /// <summary>Human-readable command description for tooltips and diagnostics.</summary>
    string Description { get; }

    /// <summary>Applies the command.</summary>
    Task ExecuteAsync();

    /// <summary>Reverts the command.</summary>
    Task UndoAsync();
}
