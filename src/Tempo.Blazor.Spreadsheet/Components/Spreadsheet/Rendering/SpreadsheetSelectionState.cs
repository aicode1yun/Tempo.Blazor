using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Rendering;

/// <summary>
/// Holds the active cell and rectangular selection state independently from any renderer.
/// </summary>
internal sealed class SpreadsheetSelectionState
{
    /// <summary>The currently active cell reference.</summary>
    public string? ActiveCellRef { get; set; }

    /// <summary>Start cell of the selection.</summary>
    public string? SelectionStartRef { get; set; }

    /// <summary>End cell of the selection.</summary>
    public string? SelectionEndRef { get; set; }

    /// <summary>Whether the selection spans more than one cell.</summary>
    public bool HasRangeSelection =>
        !string.IsNullOrEmpty(SelectionStartRef)
        && !string.IsNullOrEmpty(SelectionEndRef)
        && !string.Equals(SelectionStartRef, SelectionEndRef, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sets the active cell and collapses the selection to it.</summary>
    public void SetActiveCell(string cellRef)
    {
        ActiveCellRef = cellRef;
        SelectionStartRef = cellRef;
        SelectionEndRef = cellRef;
    }

    /// <summary>Extends the current selection to the provided cell.</summary>
    public void ExtendTo(string cellRef)
    {
        SelectionStartRef ??= ActiveCellRef ?? cellRef;
        SelectionEndRef = cellRef;
        ActiveCellRef = cellRef;
    }

    /// <summary>Gets normalized zero-based selection bounds.</summary>
    public (int StartRow, int StartCol, int EndRow, int EndCol) GetBounds()
    {
        var start = ParseCellRef(SelectionStartRef ?? ActiveCellRef ?? "A1");
        var end = ParseCellRef(SelectionEndRef ?? SelectionStartRef ?? ActiveCellRef ?? "A1");
        return (
            Math.Min(start.Row, end.Row),
            Math.Min(start.Col, end.Col),
            Math.Max(start.Row, end.Row),
            Math.Max(start.Col, end.Col));
    }

    /// <summary>Gets all A1 cell references in the selected rectangle.</summary>
    public IEnumerable<string> GetSelectedCellRefs(SpreadsheetSheet sheet)
    {
        var bounds = GetBounds();
        for (var row = Math.Max(0, bounds.StartRow); row <= Math.Min(sheet.RowCount - 1, bounds.EndRow); row++)
        {
            for (var col = Math.Max(0, bounds.StartCol); col <= Math.Min(sheet.ColumnCount - 1, bounds.EndCol); col++)
            {
                yield return ToCellRef(row, col);
            }
        }
    }

    /// <summary>Converts a zero-based row and column index to A1 notation.</summary>
    public static string ToCellRef(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    /// <summary>Parses A1 notation into zero-based row and column indices.</summary>
    public static (int Row, int Col) ParseCellRef(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = SpreadsheetRange.ColumnLettersToIndex(letters);
        var row = int.TryParse(numbers, out var r) ? r - 1 : 0;
        return (Math.Max(0, row), Math.Max(0, col));
    }
}
