using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Event arguments fired when a cell value or formula changes.</summary>
public sealed class SpreadsheetChangeEventArgs : EventArgs
{
    /// <summary>The affected sheet.</summary>
    public SpreadsheetSheet Sheet { get; }

    /// <summary>The A1 reference of the changed cell.</summary>
    public string CellRef { get; }

    /// <summary>The previous value (null if the cell did not exist).</summary>
    public object? PreviousValue { get; }

    /// <summary>The new value.</summary>
    public object? NewValue { get; }

    /// <summary>The previous formula (null if the cell did not exist).</summary>
    public string? PreviousFormula { get; }

    /// <summary>The new formula.</summary>
    public string? NewFormula { get; }

    public SpreadsheetChangeEventArgs(
        SpreadsheetSheet sheet,
        string cellRef,
        object? previousValue,
        object? newValue,
        string? previousFormula = null,
        string? newFormula = null)
    {
        Sheet = sheet;
        CellRef = cellRef;
        PreviousValue = previousValue;
        NewValue = newValue;
        PreviousFormula = previousFormula;
        NewFormula = newFormula;
    }
}
