using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Event arguments fired when a cell enters or exits edit mode.</summary>
public sealed class SpreadsheetCellEditEventArgs : EventArgs
{
    /// <summary>The affected sheet.</summary>
    public SpreadsheetSheet Sheet { get; }

    /// <summary>The A1 reference of the cell being edited.</summary>
    public string CellRef { get; }

    /// <summary>True when the cell entered edit mode; false when editing was committed or cancelled.</summary>
    public bool IsEditing { get; }

    /// <summary>The value being edited (available when IsEditing is true).</summary>
    public string? EditValue { get; }

    public SpreadsheetCellEditEventArgs(
        SpreadsheetSheet sheet,
        string cellRef,
        bool isEditing,
        string? editValue = null)
    {
        Sheet = sheet;
        CellRef = cellRef;
        IsEditing = isEditing;
        EditValue = editValue;
    }
}
