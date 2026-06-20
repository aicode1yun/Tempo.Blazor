using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Manages undo/redo stacks for spreadsheet operations.
/// </summary>
public sealed class SpreadsheetCommandManager
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<ISpreadsheetCommand> _undoStack = [];
    private readonly List<ISpreadsheetCommand> _redoStack = [];

    public SpreadsheetCommandManager(SpreadsheetSheet sheet)
    {
        _sheet = sheet;
    }

    /// <summary>Whether an undo operation is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether a redo operation is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Executes a command and adds it to the undo stack.</summary>
    public void Execute(ISpreadsheetCommand command)
    {
        command.Execute();
        _undoStack.Add(command);
        _redoStack.Clear();
    }

    /// <summary>Undoes the last command.</summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var command = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        command.Undo();
        _redoStack.Add(command);
    }

    /// <summary>Redoes the last undone command.</summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var command = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        command.Execute();
        _undoStack.Add(command);
    }
}
