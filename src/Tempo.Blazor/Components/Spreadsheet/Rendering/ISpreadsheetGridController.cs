namespace Tempo.Blazor.Components.Spreadsheet.Rendering;

/// <summary>
/// Defines the runtime operations exposed by spreadsheet grid renderers to the parent spreadsheet component.
/// </summary>
public interface ISpreadsheetGridController
{
    /// <summary>Start cell of the current selection.</summary>
    string? SelectionStartRef { get; }

    /// <summary>End cell of the current selection.</summary>
    string? SelectionEndRef { get; }

    /// <summary>Whether the renderer is currently editing a formula and can accept pointed cell references.</summary>
    bool IsInFormulaPointMode { get; }

    /// <summary>The live edit value currently held by the renderer.</summary>
    string? CurrentEditValue { get; }

    /// <summary>Focuses the grid root.</summary>
    Task FocusAsync();

    /// <summary>Moves the active cell by the supplied row/column deltas and focuses the grid.</summary>
    Task MoveActiveCellByAsync(int dRow, int dCol, bool extendSelection = false);

    /// <summary>Selects the whole sheet.</summary>
    void SelectAllCells();

    /// <summary>Gets all A1 cell references in the current selection.</summary>
    IEnumerable<string> GetSelectedCellRefs();

    /// <summary>Appends text to the live edit value.</summary>
    void AppendEditValue(string text);

    /// <summary>Inserts or replaces a formula reference in the live edit value.</summary>
    void InsertCellRefIntoFormula(string cellRef);

    /// <summary>Invalidates renderer-side cached output for the supplied cells.</summary>
    void InvalidateRenderedCells(IEnumerable<string> cellRefs);

    /// <summary>Invalidates renderer-side cached output for the supplied rows.</summary>
    void InvalidateRenderedRows(IEnumerable<int> rowIndices);

    /// <summary>Invalidates renderer-side cached output for the supplied columns.</summary>
    void InvalidateRenderedColumns(IEnumerable<int> columnIndices);

    /// <summary>Clears all renderer-side cached output.</summary>
    void ClearRenderedCache();
}
