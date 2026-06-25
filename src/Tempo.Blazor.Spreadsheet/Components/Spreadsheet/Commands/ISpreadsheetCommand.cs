namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Marker interface for spreadsheet undo/redo commands.</summary>
public interface ISpreadsheetCommand
{
    /// <summary>Executes the command.</summary>
    void Execute();

    /// <summary>Reverses the command.</summary>
    void Undo();
}
