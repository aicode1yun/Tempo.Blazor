using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Event arguments fired when the active cell or selection changes.</summary>
public sealed class SpreadsheetSelectEventArgs : EventArgs
{
    /// <summary>The affected sheet.</summary>
    public SpreadsheetSheet Sheet { get; }

    /// <summary>The A1 reference of the active cell.</summary>
    public string ActiveCellRef { get; }

    /// <summary>The start cell of the range selection (same as ActiveCellRef for single cell).</summary>
    public string? SelectionStartRef { get; }

    /// <summary>The end cell of the range selection.</summary>
    public string? SelectionEndRef { get; }

    public SpreadsheetSelectEventArgs(
        SpreadsheetSheet sheet,
        string activeCellRef,
        string? selectionStartRef = null,
        string? selectionEndRef = null)
    {
        Sheet = sheet;
        ActiveCellRef = activeCellRef;
        SelectionStartRef = selectionStartRef;
        SelectionEndRef = selectionEndRef;
    }
}
