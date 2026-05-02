using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Renders the interactive grid of a single spreadsheet sheet including row and column headers,
/// cell selection, inline editing, and keyboard navigation.
/// </summary>
public partial class TmSpreadsheetGrid
{
    private ElementReference _gridElement;
    private ElementReference _editInput;
    private string? _editValue;
    private bool _shouldFocusAfterRender;

    private bool _isAutoFillDragging;
    private string? _autoFillSourceRange;
    private string? _autoFillPreviewRange;

    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;

    private List<int> _rowIndices = [];
    private readonly Dictionary<string, string> _displayValueCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The sheet to render.</summary>
    [Parameter] public SpreadsheetSheet? Sheet { get; set; }

    /// <summary>Default row height in pixels.</summary>
    [Parameter] public double RowHeight { get; set; } = 20;

    /// <summary>Default column width in pixels.</summary>
    [Parameter] public double ColumnWidth { get; set; } = 64;

    /// <summary>Called when the active cell changes.</summary>
    [Parameter] public EventCallback<string?> ActiveCellChanged { get; set; }

    /// <summary>Called when a cell value is committed after editing.</summary>
    [Parameter] public EventCallback<(string CellRef, string? Value)> CellValueCommitted { get; set; }

    /// <summary>Called when the user requests a copy operation (Ctrl+C).</summary>
    [Parameter] public EventCallback OnCopyRequested { get; set; }

    /// <summary>Called when the user requests a paste operation (Ctrl+V).</summary>
    [Parameter] public EventCallback OnPasteRequested { get; set; }

    /// <summary>Called when the user requests a cut operation (Ctrl+X).</summary>
    [Parameter] public EventCallback OnCutRequested { get; set; }

    /// <summary>Called when the user requests insert row from context menu.</summary>
    [Parameter] public EventCallback OnInsertRowRequested { get; set; }

    /// <summary>Called when the user requests delete row from context menu.</summary>
    [Parameter] public EventCallback OnDeleteRowRequested { get; set; }

    /// <summary>Called when the user requests insert column from context menu.</summary>
    [Parameter] public EventCallback OnInsertColumnRequested { get; set; }

    /// <summary>Called when the user requests delete column from context menu.</summary>
    [Parameter] public EventCallback OnDeleteColumnRequested { get; set; }

    /// <summary>Called when the user requests delete selection (Delete key).</summary>
    [Parameter] public EventCallback OnDeleteRequested { get; set; }

    /// <summary>Called when the user requests undo (Ctrl+Z).</summary>
    [Parameter] public EventCallback OnUndoRequested { get; set; }

    /// <summary>Called when the user requests redo (Ctrl+Y).</summary>
    [Parameter] public EventCallback OnRedoRequested { get; set; }

    /// <summary>Called when the user requests bold toggle (Ctrl+B).</summary>
    [Parameter] public EventCallback OnBoldToggleRequested { get; set; }

    /// <summary>Called when the user requests italic toggle (Ctrl+I).</summary>
    [Parameter] public EventCallback OnItalicToggleRequested { get; set; }

    /// <summary>Called when the user requests underline toggle (Ctrl+U).</summary>
    [Parameter] public EventCallback OnUnderlineToggleRequested { get; set; }

    /// <summary>Called when the user requests select all (Ctrl+A).</summary>
    [Parameter] public EventCallback OnSelectAllRequested { get; set; }

    /// <summary>Called when a cell enters or exits edit mode.</summary>
    [Parameter] public EventCallback<SpreadsheetCellEditEventArgs> OnCellEdit { get; set; }

    /// <summary>Called when the user clicks another cell while editing a formula, to insert its reference.</summary>
    [Parameter] public EventCallback<string> OnCellReferenceRequested { get; set; }

    /// <summary>Whether a cell is currently being edited.</summary>
    public bool IsEditing { get; private set; }

    /// <summary>Whether row virtualization is active (no freeze rows, not editing).</summary>
    private bool UseVirtualization => Sheet?.FreezeRowCount == 0 && !IsEditing;

    /// <summary>Gets the currently active cell reference.</summary>
    public string? ActiveCellRef => Sheet?.ActiveCellRef;

    /// <summary>Focuses the grid element.</summary>
    public async Task FocusAsync()
    {
        try { await _gridElement.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
    }

    /// <summary>Computes the average row height for virtualization item sizing.</summary>
    private float GetAverageRowHeight()
    {
        if (Sheet is null) return (float)RowHeight;
        var sum = 0.0;
        for (int r = 0; r < Math.Min(Sheet.RowCount, 100); r++)
            sum += GetRowHeight(r);
        return (float)(sum / Math.Min(Sheet.RowCount, 100));
    }

    protected override void OnParametersSet()
    {
        if (Sheet is not null && _rowIndices.Count != Sheet.RowCount)
        {
            _rowIndices = Enumerable.Range(0, Sheet.RowCount).ToList();
        }
    }

    /// <summary>Start cell of a range selection.</summary>
    public string? SelectionStartRef { get; private set; }

    /// <summary>End cell of a range selection.</summary>
    public string? SelectionEndRef { get; private set; }

    /// <summary>Whether a range is currently selected.</summary>
    public bool HasRangeSelection => !string.IsNullOrEmpty(SelectionStartRef) && !string.IsNullOrEmpty(SelectionEndRef)
        && SelectionStartRef != SelectionEndRef;

    /// <summary>
    /// Gets the ordered selection bounds as zero-based (startRow, startCol, endRow, endCol).
    /// </summary>
    private (int StartRow, int StartCol, int EndRow, int EndCol) GetSelectionBounds()
    {
        var start = ParseCellRef(SelectionStartRef ?? Sheet?.ActiveCellRef ?? "A1");
        var end = ParseCellRef(SelectionEndRef ?? SelectionStartRef ?? Sheet?.ActiveCellRef ?? "A1");
        return (
            Math.Min(start.Row, end.Row),
            Math.Min(start.Col, end.Col),
            Math.Max(start.Row, end.Row),
            Math.Max(start.Col, end.Col)
        );
    }

    private static (int Row, int Col) ParseCellRef(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = SpreadsheetRange.ColumnLettersToIndex(letters);
        var row = int.TryParse(numbers, out var r) ? r - 1 : 0;
        return (row, col);
    }

    private static string ToCellRef(int row, int col)
    {
        return $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
    }

    /// <summary>Gets all cell references in the current selection (including active cell).</summary>
    public IEnumerable<string> GetSelectedCellRefs()
    {
        if (Sheet is null) yield break;
        var bounds = GetSelectionBounds();
        for (var r = bounds.StartRow; r <= bounds.EndRow; r++)
        {
            for (var c = bounds.StartCol; c <= bounds.EndCol; c++)
            {
                yield return ToCellRef(r, c);
            }
        }
    }

    private bool IsActiveCell(string cellRef)
    {
        return Sheet?.ActiveCellRef?.Equals(cellRef, StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool IsCellSelected(string cellRef)
    {
        if (Sheet?.ActiveCellRef?.Equals(cellRef, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (!HasRangeSelection)
            return false;

        var bounds = GetSelectionBounds();
        var (row, col) = ParseCellRef(cellRef);
        return row >= bounds.StartRow && row <= bounds.EndRow && col >= bounds.StartCol && col <= bounds.EndCol;
    }

    private string GetCellClass(bool isActive, bool isSelected, bool isMergedHidden)
    {
        var classes = new System.Text.StringBuilder();
        if (isActive) classes.Append(" tm-spreadsheet-cell--active");
        if (isSelected) classes.Append(" tm-spreadsheet-cell--selected");
        if (isMergedHidden) classes.Append(" tm-spreadsheet-cell--merged-hidden");
        return classes.ToString().Trim();
    }

    private SpreadsheetRange? GetMergedRangeStart(int row, int col)
    {
        if (Sheet?.MergedCells is null) return null;
        foreach (var range in Sheet.MergedCells)
        {
            if (range.StartRow == row && range.StartCol == col)
                return range;
        }
        return null;
    }

    private SpreadsheetRange? GetMergedRangeCovering(int row, int col)
    {
        if (Sheet?.MergedCells is null) return null;
        foreach (var range in Sheet.MergedCells)
        {
            if (range.Contains(row, col) && !(range.StartRow == row && range.StartCol == col))
                return range;
        }
        return null;
    }

    private bool IsCellMergedAndHidden(int row, int col) => GetMergedRangeCovering(row, col) is not null;

    private bool IsSelectionEndCell(int row, int col)
    {
        if (!HasRangeSelection) return Sheet?.ActiveCellRef == $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        var bounds = GetSelectionBounds();
        return row == bounds.EndRow && col == bounds.EndCol;
    }

    private string GetRowHeaderClass(int rowIndex)
    {
        if (!HasRangeSelection) return string.Empty;
        var bounds = GetSelectionBounds();
        return rowIndex >= bounds.StartRow && rowIndex <= bounds.EndRow ? "tm-spreadsheet-header-cell--selected" : string.Empty;
    }

    private string GetColumnHeaderClass(int colIndex)
    {
        if (!HasRangeSelection) return string.Empty;
        var bounds = GetSelectionBounds();
        return colIndex >= bounds.StartCol && colIndex <= bounds.EndCol ? "tm-spreadsheet-header-cell--selected" : string.Empty;
    }

    private double GetRowHeight(int rowIndex)
    {
        if (Sheet?.Rows.TryGetValue(rowIndex, out var row) == true && row.Height.HasValue)
            return row.Height.Value;
        return RowHeight;
    }

    private double GetColumnWidth(int colIndex)
    {
        if (Sheet?.Columns.TryGetValue(colIndex, out var col) == true && col.Width.HasValue)
            return col.Width.Value;
        return ColumnWidth;
    }

    private double GetCumulativeRowHeight(int upToRow)
    {
        var sum = 0.0;
        for (int r = 0; r < upToRow && r < Sheet?.RowCount; r++)
            sum += GetRowHeight(r);
        return sum;
    }

    private double GetCumulativeColumnWidth(int upToCol)
    {
        var sum = 0.0;
        for (int c = 0; c < upToCol && c < Sheet?.ColumnCount; c++)
            sum += GetColumnWidth(c);
        return sum;
    }

    private bool IsFrozenRow(int rowIndex) => Sheet?.FreezeRowCount > 0 && rowIndex < Sheet.FreezeRowCount;
    private bool IsFrozenCol(int colIndex) => Sheet?.FreezeColumnCount > 0 && colIndex < Sheet.FreezeColumnCount;

    private string GetFreezeCellStyle(int rowIndex, int colIndex)
    {
        var sb = new System.Text.StringBuilder();
        if (IsFrozenRow(rowIndex))
        {
            var top = 20 + GetCumulativeRowHeight(rowIndex); // 20 = header height
            sb.Append($"position: sticky; top: {top}px;");
        }
        if (IsFrozenCol(colIndex))
        {
            var left = 40 + GetCumulativeColumnWidth(colIndex); // 40 = row header width
            sb.Append($" position: sticky; left: {left}px;");
        }
        if (IsFrozenRow(rowIndex) || IsFrozenCol(colIndex))
        {
            var z = 0;
            if (IsFrozenRow(rowIndex) && IsFrozenCol(colIndex)) z = 3;
            else if (IsFrozenRow(rowIndex)) z = 2;
            else z = 1;
            sb.Append($" z-index: {z};");
        }
        return sb.ToString().Trim();
    }

    private string GetColumnHeaderFreezeStyle(int colIndex)
    {
        if (!IsFrozenCol(colIndex)) return string.Empty;
        var left = 40 + GetCumulativeColumnWidth(colIndex);
        return $" position: sticky; left: {left}px; z-index: 3;";
    }

    private string GetRowHeaderFreezeStyle(int rowIndex)
    {
        if (!IsFrozenRow(rowIndex)) return string.Empty;
        var top = 20 + GetCumulativeRowHeight(rowIndex);
        return $" position: sticky; top: {top}px; z-index: 3;";
    }

    private string GetCellDisplayValue(string cellRef, SpreadsheetCell? cell)
    {
        if (cell is null)
            return string.Empty;

        var cacheKey = $"{cellRef}|{cell.GetHashCode()}|{cell.Value}|{cell.Formula}|{cell.Style.NumberFormat}";
        if (_displayValueCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Lazy formula evaluation – evaluate on first render if not already done
        if (!string.IsNullOrEmpty(cell.Formula) && (cell.DisplayValue is null || cell.Value is null))
        {
            Sheet?.EvaluateFormula(cellRef);
        }

        var displayValue = !string.IsNullOrEmpty(cell.DisplayValue)
            ? cell.DisplayValue
            : SpreadsheetNumberFormatter.Format(cell.Value, cell.Style.NumberFormat);

        _displayValueCache[cacheKey] = displayValue ?? string.Empty;
        return displayValue ?? string.Empty;
    }

    /// <summary>Clears the display value cache. Call after bulk data changes.</summary>
    public void ClearDisplayValueCache()
    {
        _displayValueCache.Clear();
    }

    /// <summary>Builds the inline CSS style string for a cell including dimensions, font, colors, alignment and borders.</summary>
    private string GetCellStyleString(SpreadsheetCell? cell, int colIndex, int rowIndex)
    {
        var style = cell?.Style;
        var sb = new System.Text.StringBuilder();
        var merged = GetMergedRangeStart(rowIndex, colIndex);
        var width = GetColumnWidth(colIndex);
        var height = GetRowHeight(rowIndex);
        if (merged is not null)
        {
            for (int c = merged.StartCol + 1; c <= merged.EndCol; c++)
                width += GetColumnWidth(c);
            for (int r = merged.StartRow + 1; r <= merged.EndRow; r++)
                height += GetRowHeight(r);
        }
        sb.Append($"width: {width}px; height: {height}px;");
        var freezeStyle = GetFreezeCellStyle(rowIndex, colIndex);
        if (!string.IsNullOrEmpty(freezeStyle))
            sb.Append($" {freezeStyle}");

        if (style is not null)
        {
            // Font
            sb.Append($" font-family: {style.FontFamily}; font-size: {style.FontSize}pt;");
            if (style.Bold) sb.Append(" font-weight: bold;");
            if (style.Italic) sb.Append(" font-style: italic;");
            if (style.Underline) sb.Append(" text-decoration: underline;");

            // Colors
            if (!string.IsNullOrEmpty(style.ForeColor) && style.ForeColor != "#000000")
                sb.Append($" color: {style.ForeColor};");
            if (!string.IsNullOrEmpty(style.BackgroundColor) && style.BackgroundColor != "transparent")
                sb.Append($" background-color: {style.BackgroundColor};");

            // Alignment
            sb.Append($" justify-content: {GetJustifyContent(style.HorizontalAlign)};");
            sb.Append($" align-items: {GetAlignItems(style.VerticalAlign)};");
            if (style.TextWrap)
                sb.Append(" white-space: normal; word-break: break-word;");

            // Borders
            AppendBorderStyle(sb, "border-top", style.BorderTop);
            AppendBorderStyle(sb, "border-right", style.BorderRight);
            AppendBorderStyle(sb, "border-bottom", style.BorderBottom);
            AppendBorderStyle(sb, "border-left", style.BorderLeft);
        }

        return sb.ToString();
    }

    private static void AppendBorderStyle(System.Text.StringBuilder sb, string property, SpreadsheetBorder border)
    {
        if (border.Style == Enums.SpreadsheetBorderStyle.None) return;
        var cssStyle = border.Style switch
        {
            Enums.SpreadsheetBorderStyle.Thin => "1px solid",
            Enums.SpreadsheetBorderStyle.Medium => "2px solid",
            Enums.SpreadsheetBorderStyle.Thick => "3px solid",
            Enums.SpreadsheetBorderStyle.Dashed => "1px dashed",
            Enums.SpreadsheetBorderStyle.Dotted => "1px dotted",
            Enums.SpreadsheetBorderStyle.Double => "double",
            _ => "1px solid"
        };
        sb.Append($" {property}: {cssStyle} {border.Color};");
    }

    private static string GetJustifyContent(Enums.SpreadsheetHorizontalAlign align) => align switch
    {
        Enums.SpreadsheetHorizontalAlign.Center => "center",
        Enums.SpreadsheetHorizontalAlign.Right => "flex-end",
        _ => "flex-start"
    };

    private static string GetAlignItems(Enums.SpreadsheetVerticalAlign align) => align switch
    {
        Enums.SpreadsheetVerticalAlign.Top => "flex-start",
        Enums.SpreadsheetVerticalAlign.Middle => "center",
        _ => "flex-end"
    };

    private string? GetEditValue(SpreadsheetCell? cell)
    {
        if (_editValue is not null)
            return _editValue;
        if (cell?.Formula is not null)
            return cell.Formula;
        return cell?.Value?.ToString() ?? string.Empty;
    }

    private void OnCellClick(string cellRef, MouseEventArgs e)
    {
        if (_isAutoFillDragging) return;

        if (IsEditing && IsActiveCell(cellRef))
            return;

        if (IsEditing)
        {
            // If editing a formula, clicking another cell inserts its reference
            if (_editValue?.StartsWith("=") == true)
            {
                OnCellReferenceRequested.InvokeAsync(cellRef);
                return;
            }
            CommitEdit();
        }

        if (e.ShiftKey && !string.IsNullOrEmpty(SelectionStartRef))
        {
            SelectionEndRef = cellRef;
            Sheet!.ActiveCellRef = cellRef;
        }
        else if (e.CtrlKey)
        {
            // Multi-selection: for now just activate the cell
            // Full multi-selection with disjoint ranges is future work
            Sheet!.ActiveCellRef = cellRef;
            SelectionStartRef = cellRef;
            SelectionEndRef = cellRef;
        }
        else
        {
            Sheet!.ActiveCellRef = cellRef;
            SelectionStartRef = cellRef;
            SelectionEndRef = cellRef;
        }

        ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
        _shouldFocusAfterRender = true;
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
    }

    private void CloseContextMenu()
    {
        _contextMenuVisible = false;
    }

    private void ContextMenuCopy()
    {
        CloseContextMenu();
        OnCopyRequested.InvokeAsync();
    }

    private void ContextMenuCut()
    {
        CloseContextMenu();
        OnCutRequested.InvokeAsync();
    }

    private void ContextMenuPaste()
    {
        CloseContextMenu();
        OnPasteRequested.InvokeAsync();
    }

    private void ContextMenuInsertRow()
    {
        CloseContextMenu();
        OnInsertRowRequested.InvokeAsync();
    }

    private void ContextMenuDeleteRow()
    {
        CloseContextMenu();
        OnDeleteRowRequested.InvokeAsync();
    }

    private void ContextMenuInsertColumn()
    {
        CloseContextMenu();
        OnInsertColumnRequested.InvokeAsync();
    }

    private void ContextMenuDeleteColumn()
    {
        CloseContextMenu();
        OnDeleteColumnRequested.InvokeAsync();
    }

    private void OnAutoFillMouseDown(MouseEventArgs e)
    {
        if (Sheet is null) return;
        _isAutoFillDragging = true;
        var bounds = GetSelectionBounds();
        _autoFillSourceRange = $"{ToCellRef(bounds.StartRow, bounds.StartCol)}:{ToCellRef(bounds.EndRow, bounds.EndCol)}";
        _autoFillPreviewRange = _autoFillSourceRange;
    }

    private void OnAutoFillMouseMove(MouseEventArgs e)
    {
        if (!_isAutoFillDragging || Sheet is null) return;

        // Calculate target cell from mouse position relative to grid
        // Approximate: use client coordinates and cell dimensions
        var (row, col) = HitTestCell(e.ClientX, e.ClientY);
        if (row < 0 || col < 0) return;

        var bounds = GetSelectionBounds();
        var startRow = bounds.StartRow;
        var startCol = bounds.StartCol;
        var endRow = Math.Max(row, bounds.EndRow);
        var endCol = Math.Max(col, bounds.EndCol);

        // Only expand in one direction based on original selection shape
        var selRows = bounds.EndRow - bounds.StartRow + 1;
        var selCols = bounds.EndCol - bounds.StartCol + 1;
        if (selRows > selCols || (selRows == 1 && selCols == 1 && endRow > bounds.EndRow))
        {
            _autoFillPreviewRange = $"{ToCellRef(startRow, startCol)}:{ToCellRef(endRow, startCol)}";
        }
        else
        {
            _autoFillPreviewRange = $"{ToCellRef(startRow, startCol)}:{ToCellRef(startRow, endCol)}";
        }
        StateHasChanged();
    }

    private void OnAutoFillMouseUp(MouseEventArgs e)
    {
        if (!_isAutoFillDragging || Sheet is null || _autoFillSourceRange is null || _autoFillPreviewRange is null)
        {
            _isAutoFillDragging = false;
            _autoFillPreviewRange = null;
            return;
        }

        _isAutoFillDragging = false;

        if (_autoFillPreviewRange != _autoFillSourceRange)
        {
            var cmd = new Commands.AutoFillCommand(Sheet, _autoFillSourceRange, _autoFillPreviewRange);
            // For now invoke directly since command manager is in parent
            cmd.Execute();
        }

        _autoFillPreviewRange = null;
        _shouldFocusAfterRender = true;
        StateHasChanged();
    }

    private (int Row, int Col) HitTestCell(double clientX, double clientY)
    {
        // Simplified hit-test: assumes grid starts at (0,0) in viewport
        // A proper implementation would use JS interop to get bounding rect
        if (Sheet is null) return (-1, -1);
        var headerHeight = 20;
        var rowHeaderWidth = 40;
        var y = clientY - headerHeight;
        var x = clientX - rowHeaderWidth;
        if (y < 0 || x < 0) return (-1, -1);

        var row = 0;
        var accumulatedY = 0.0;
        while (row < Sheet.RowCount && accumulatedY + GetRowHeight(row) < y)
        {
            accumulatedY += GetRowHeight(row);
            row++;
        }

        var col = 0;
        var accumulatedX = 0.0;
        while (col < Sheet.ColumnCount && accumulatedX + GetColumnWidth(col) < x)
        {
            accumulatedX += GetColumnWidth(col);
            col++;
        }

        return (Math.Min(row, Sheet.RowCount - 1), Math.Min(col, Sheet.ColumnCount - 1));
    }

    private void SelectRow(int rowIndex)
    {
        if (IsEditing) CommitEdit();
        var startRef = ToCellRef(rowIndex, 0);
        var endRef = ToCellRef(rowIndex, Sheet!.ColumnCount - 1);
        Sheet!.ActiveCellRef = startRef;
        SelectionStartRef = startRef;
        SelectionEndRef = endRef;
        ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
    }

    private void SelectColumn(int colIndex)
    {
        if (IsEditing) CommitEdit();
        var startRef = ToCellRef(0, colIndex);
        var endRef = ToCellRef(Sheet!.RowCount - 1, colIndex);
        Sheet!.ActiveCellRef = startRef;
        SelectionStartRef = startRef;
        SelectionEndRef = endRef;
        ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
    }

    public void SelectAllCells()
    {
        if (IsEditing) CommitEdit();
        var startRef = ToCellRef(0, 0);
        var endRef = ToCellRef(Sheet!.RowCount - 1, Sheet.ColumnCount - 1);
        Sheet!.ActiveCellRef = startRef;
        SelectionStartRef = startRef;
        SelectionEndRef = endRef;
        ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
    }

    private void StartEdit(string cellRef)
    {
        StartEdit(cellRef, null);
    }

    private void StartEdit(string cellRef, string? initialValue)
    {
        if (Sheet?.ActiveCellRef != cellRef)
        {
            Sheet!.ActiveCellRef = cellRef;
            SelectionStartRef = cellRef;
            SelectionEndRef = cellRef;
            ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
        }

        IsEditing = true;
        _editValue = initialValue;
        _shouldFocusAfterRender = true;
        OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet!, Sheet.ActiveCellRef!, true));
    }

    /// <summary>Appends text to the current edit value. Used for formula cell reference insertion.</summary>
    public void AppendEditValue(string text)
    {
        _editValue = (_editValue ?? string.Empty) + text;
        _shouldFocusAfterRender = true;
        StateHasChanged();
    }

    private void OnEditInput(ChangeEventArgs e)
    {
        _editValue = e.Value?.ToString();
    }

    private void HandleEditKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                CommitEdit();
                MoveActiveCell(e.ShiftKey ? -1 : 1, 0); // vertical
                break;
            case "Escape":
                CancelEdit();
                break;
            case "Tab":
                CommitEdit();
                MoveActiveCell(0, e.ShiftKey ? -1 : 1); // horizontal
                break;
        }
    }

    private void CommitEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
        var cellRef = Sheet?.ActiveCellRef;
        if (cellRef is not null && _editValue is not null)
        {
            CellValueCommitted.InvokeAsync((cellRef, _editValue));
            ClearDisplayValueCache();
        }
        _editValue = null;
        _shouldFocusAfterRender = true;
        OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet!, cellRef ?? "A1", false));
        StateHasChanged();
    }

    private void CancelEdit()
    {
        IsEditing = false;
        _editValue = null;
        _shouldFocusAfterRender = true;
        OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet!, Sheet?.ActiveCellRef ?? "A1", false));
        StateHasChanged();
    }

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (IsEditing) return;

        if (e.CtrlKey)
        {
            switch (e.Key)
            {
                case "c":
                    OnCopyRequested.InvokeAsync();
                    return;
                case "v":
                    OnPasteRequested.InvokeAsync();
                    return;
                case "x":
                    OnCutRequested.InvokeAsync();
                    return;
                case "z":
                    OnUndoRequested.InvokeAsync();
                    return;
                case "y":
                    OnRedoRequested.InvokeAsync();
                    return;
                case "b":
                    OnBoldToggleRequested.InvokeAsync();
                    return;
                case "i":
                    OnItalicToggleRequested.InvokeAsync();
                    return;
                case "u":
                    OnUnderlineToggleRequested.InvokeAsync();
                    return;
                case "a":
                    OnSelectAllRequested.InvokeAsync();
                    return;
                case "Home":
                    MoveToCell(0, 0, e.ShiftKey);
                    return;
                case "End":
                    MoveToLastUsedCell(e.ShiftKey);
                    return;
            }
        }

        switch (e.Key)
        {
            case "ArrowUp":
                MoveActiveCell(-1, 0, e.ShiftKey);
                break;
            case "ArrowDown":
                MoveActiveCell(1, 0, e.ShiftKey);
                break;
            case "ArrowLeft":
                MoveActiveCell(0, -1, e.ShiftKey);
                break;
            case "ArrowRight":
                MoveActiveCell(0, 1, e.ShiftKey);
                break;
            case "Enter":
                StartEdit(Sheet?.ActiveCellRef ?? "A1");
                break;
            case "F2":
                StartEdit(Sheet?.ActiveCellRef ?? "A1");
                break;
            case "Tab":
                MoveActiveCell(0, e.ShiftKey ? -1 : 1);
                break;
            case "Escape":
                SelectionEndRef = SelectionStartRef;
                break;
            case "Delete":
                OnDeleteRequested.InvokeAsync();
                break;
            case "Home":
                MoveToCell(Sheet?.ActiveCellRef is {} acr ? ParseCellRef(acr).Row : 0, 0, e.ShiftKey);
                break;
            case "End":
                if (Sheet is not null)
                {
                    var (activeRow, _) = ParseCellRef(Sheet.ActiveCellRef ?? "A1");
                    MoveToCell(activeRow, Sheet.ColumnCount - 1, e.ShiftKey);
                }
                break;
            default:
                // Auto-start edit mode on printable character (length 1, not a control key)
                if (e.Key.Length == 1 && !e.AltKey && !e.CtrlKey && !e.MetaKey)
                {
                    StartEdit(Sheet?.ActiveCellRef ?? "A1", e.Key);
                }
                break;
        }
    }

    private void MoveActiveCell(int dRow, int dCol, bool extendSelection = false)
    {
        if (Sheet is null) return;
        var (row, col) = ParseCellRef(Sheet.ActiveCellRef ?? "A1");
        var newRow = Math.Clamp(row + dRow, 0, Sheet.RowCount - 1);
        var newCol = Math.Clamp(col + dCol, 0, Sheet.ColumnCount - 1);
        MoveToCell(newRow, newCol, extendSelection);
    }

    private void MoveToCell(int row, int col, bool extendSelection = false)
    {
        if (Sheet is null) return;
        var newRef = ToCellRef(row, col);

        Sheet.ActiveCellRef = newRef;
        if (extendSelection && !string.IsNullOrEmpty(SelectionStartRef))
        {
            SelectionEndRef = newRef;
        }
        else
        {
            SelectionStartRef = newRef;
            SelectionEndRef = newRef;
        }
        ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
    }

    private void MoveToLastUsedCell(bool extendSelection = false)
    {
        if (Sheet is null) return;
        var lastRow = 0;
        var lastCol = 0;
        foreach (var cellRef in Sheet.Cells.Keys)
        {
            var (row, col) = ParseCellRef(cellRef);
            if (row > lastRow) lastRow = row;
            if (col > lastCol) lastCol = col;
        }
        MoveToCell(lastRow, lastCol, extendSelection);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldFocusAfterRender)
        {
            _shouldFocusAfterRender = false;
            if (IsEditing)
            {
                try { await _editInput.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
            }
            else
            {
                try { await _gridElement.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
            }
        }
    }
}
