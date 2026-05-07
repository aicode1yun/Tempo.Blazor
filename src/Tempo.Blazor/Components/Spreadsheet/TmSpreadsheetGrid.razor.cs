using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Runtime.CompilerServices;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Rendering;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Renders the interactive grid of a single spreadsheet sheet including row and column headers,
/// cell selection, inline editing, and keyboard navigation.
/// </summary>
public partial class TmSpreadsheetGrid : IAsyncDisposable, ISpreadsheetGridController
{
    private ElementReference _gridElement;
    private ElementReference _editInput;
    private string? _editValue;
    private bool _shouldFocusAfterRender;
    private (int Row, int Col)? _pendingVisibleCell;

    private bool _isAutoFillDragging;
    private string? _autoFillSourceRange;
    private string? _autoFillPreviewRange;

    private bool _isResizingCol;
    private int _resizingColIndex;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private double _resizePreviewWidth;

    private bool _isResizingRow;
    private int _resizingRowIndex;
    private double _resizeStartY;
    private double _resizeStartHeight;
    private double _resizePreviewHeight;

    private bool _showColWidthDialog;
    private bool _showRowHeightDialog;
    private int _contextMenuColIndex;
    private int _contextMenuRowIndex;
    private string _colWidthInputValue = "";
    private string _rowHeightInputValue = "";

    // Formula-point mode — range drag
    private bool _isFormulaPointDragging;
    private string? _formulaPointDragAnchor;
    private string? _formulaPointDragCurrent;

    // When true, the next blur on the edit input must not commit (formula-point click on another cell)
    private bool _suppressNextBlurCommit;

    // Formula-point mode — reference colour cache
    private readonly Dictionary<string, int> _formulaRefColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(int Sr, int Sc, int Er, int Ec, int Ci)> _formulaRangeColors = [];
    private const int FormulaRefColorCount = 6;

    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;

    private List<int> _rowIndices = [];
    private readonly Dictionary<string, string> _displayValueCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _cellStyleCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int Row, int Col), SpreadsheetRange> _mergedStartLookup = [];
    private readonly HashSet<(int Row, int Col)> _mergedHiddenLookup = [];
    private double[] _rowHeights = [];
    private double[] _columnWidths = [];
    private double[] _rowOffsets = [];
    private double[] _columnOffsets = [];
    private string[] _columnLetters = [];
    private double _horizontalScrollLeft;
    private double _viewportWidth = DefaultHorizontalViewportWidth;
    private DotNetObjectReference<TmSpreadsheetGrid>? _dotNetRef;
    private bool _viewportObserverRegistered;
    private SpreadsheetSheet? _cachedSheet;
    private int _cachedRowCount;
    private int _cachedColumnCount;
    private double _cachedRowHeight;
    private double _cachedColumnWidth;
    private int _cachedRowsHash;
    private int _cachedColumnsHash;
    private int _cachedMergedHash;
    private (string? StartRef, string? EndRef, string? ActiveRef, (int StartRow, int StartCol, int EndRow, int EndCol) Bounds)? _selectionBoundsCache;
    private const double RowHeaderWidth = 40;
    private const double ColumnHeaderHeight = 20;
    private const double DefaultHorizontalViewportWidth = 1024;
    private const int HorizontalOverscanColumnCount = 3;
    private const int HorizontalVirtualizationColumnThreshold = 32;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The sheet to render.</summary>
    [Parameter] public SpreadsheetSheet? Sheet { get; set; }

    /// <summary>Default row height in pixels.</summary>
    [Parameter] public double RowHeight { get; set; } = 20;

    /// <summary>Default column width in pixels.</summary>
    [Parameter] public double ColumnWidth { get; set; } = 64;

    /// <summary>Called when the active cell changes.</summary>
    [Parameter] public EventCallback<string?> ActiveCellChanged { get; set; }

    /// <summary>The external formula editor value when reference-picking is driven by the formula bar instead of inline cell editing.</summary>
    [Parameter] public string? ExternalFormulaEditValue { get; set; }

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

    /// <summary>Called when the user resizes a column (drag or dialog). Parent should apply ResizeColumnCommand.</summary>
    [Parameter] public EventCallback<(int ColIndex, double Width)> OnColumnResizeRequested { get; set; }

    /// <summary>Called when the user resizes a row (drag or dialog). Parent should apply ResizeRowCommand.</summary>
    [Parameter] public EventCallback<(int RowIndex, double Height)> OnRowResizeRequested { get; set; }

    /// <summary>Called when the user requests the Format Cells dialog (Ctrl+1).</summary>
    [Parameter] public EventCallback OnFormatCellsRequested { get; set; }

    /// <summary>Called when the user requests strikethrough toggle (Ctrl+5).</summary>
    [Parameter] public EventCallback OnStrikeThroughToggleRequested { get; set; }

    /// <summary>Whether Format Painter mode is currently active.</summary>
    [Parameter] public bool IsFormatPainterActive { get; set; }

    /// <summary>Called when the user clicks a cell while Format Painter is active.</summary>
    [Parameter] public EventCallback<string> OnFormatPainterApply { get; set; }

    /// <summary>Called when the user presses Escape while Format Painter is active.</summary>
    [Parameter] public EventCallback OnFormatPainterCancel { get; set; }

    /// <summary>Called when the user requests to hide selected rows (context menu).</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnHideRowsRequested { get; set; }

    /// <summary>Called when the user requests to unhide rows near the selection.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnUnhideRowsRequested { get; set; }

    /// <summary>Called when the user requests to hide selected columns (context menu).</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnHideColumnsRequested { get; set; }

    /// <summary>Called when the user requests to unhide columns near the selection.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnUnhideColumnsRequested { get; set; }

    /// <summary>Called when the user requests to activate Format Painter from the context menu.</summary>
    [Parameter] public EventCallback OnFormatPainterActivateRequested { get; set; }

    /// <summary>Called when the user requests to clear formatting from the selection (context menu).</summary>
    [Parameter] public EventCallback OnClearFormattingRequested { get; set; }

    /// <summary>Called when the user requests to clear cell content (values/formulas) from the selection (context menu).</summary>
    [Parameter] public EventCallback OnClearContentRequested { get; set; }

    /// <summary>Called when the user requests to clear everything (values, formulas, and formatting) from the selection (context menu).</summary>
    [Parameter] public EventCallback OnClearAllRequested { get; set; }

    /// <summary>Whether a cell is currently being edited.</summary>
    public bool IsEditing { get; private set; }

    /// <summary>True while editing a formula (value starts with '=').</summary>
    public bool IsInFormulaPointMode => (IsEditing && _editValue?.StartsWith("=") == true)
        || ExternalFormulaEditValue?.StartsWith("=") == true;

    /// <summary>Gets the current live edit value (not yet committed to the cell).</summary>
    public string? CurrentEditValue => IsEditing ? _editValue : ExternalFormulaEditValue;

    /// <summary>Whether row virtualization is active (no freeze rows).</summary>
    private bool UseVirtualization => Sheet?.FreezeRowCount == 0;

    private bool UseHorizontalVirtualization => Sheet?.ColumnCount > HorizontalVirtualizationColumnThreshold;

    /// <summary>Gets the currently active cell reference.</summary>
    public string? ActiveCellRef => Sheet?.ActiveCellRef;

    /// <summary>Focuses the grid element.</summary>
    public async Task FocusAsync()
    {
        try { await _gridElement.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
    }

    /// <inheritdoc />
    public async Task MoveActiveCellByAsync(int dRow, int dCol, bool extendSelection = false)
    {
        MoveActiveCell(dRow, dCol, extendSelection);
        await FocusAsync();
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

        RefreshRenderCachesIfNeeded();
    }

    private void RefreshRenderCachesIfNeeded()
    {
        if (Sheet is null)
        {
            ClearRenderCaches();
            return;
        }

        var rowsHash = ComputeRowsHash();
        var columnsHash = ComputeColumnsHash();
        var mergedHash = ComputeMergedCellsHash();
        var geometryChanged = _cachedSheet is not null
            && ReferenceEquals(_cachedSheet, Sheet)
            && (_cachedRowCount != Sheet.RowCount
                || _cachedColumnCount != Sheet.ColumnCount
                || Math.Abs(_cachedRowHeight - RowHeight) > double.Epsilon
                || Math.Abs(_cachedColumnWidth - ColumnWidth) > double.Epsilon
                || _cachedRowsHash != rowsHash
                || _cachedColumnsHash != columnsHash);

        var shouldRebuild = !ReferenceEquals(_cachedSheet, Sheet)
            || geometryChanged
            || _cachedRowCount != Sheet.RowCount
            || _cachedColumnCount != Sheet.ColumnCount
            || Math.Abs(_cachedRowHeight - RowHeight) > double.Epsilon
            || Math.Abs(_cachedColumnWidth - ColumnWidth) > double.Epsilon
            || _cachedRowsHash != rowsHash
            || _cachedColumnsHash != columnsHash
            || _cachedMergedHash != mergedHash;

        if (!shouldRebuild)
            return;

        _cachedSheet = Sheet;
        _cachedRowCount = Sheet.RowCount;
        _cachedColumnCount = Sheet.ColumnCount;
        _cachedRowHeight = RowHeight;
        _cachedColumnWidth = ColumnWidth;
        _cachedRowsHash = rowsHash;
        _cachedColumnsHash = columnsHash;
        _cachedMergedHash = mergedHash;

        RebuildGeometryCache();
        RebuildColumnLetterCache();
        RebuildMergedCellCache();

        if (geometryChanged)
            _cellStyleCache.Clear();
    }

    private void ClearRenderCaches()
    {
        _cachedSheet = null;
        _cachedRowCount = 0;
        _cachedColumnCount = 0;
        _cachedRowsHash = 0;
        _cachedColumnsHash = 0;
        _cachedMergedHash = 0;
        _rowHeights = [];
        _columnWidths = [];
        _rowOffsets = [];
        _columnOffsets = [];
        _columnLetters = [];
        _horizontalScrollLeft = 0;
        _viewportWidth = DefaultHorizontalViewportWidth;
        _mergedStartLookup.Clear();
        _mergedHiddenLookup.Clear();
        _selectionBoundsCache = null;
        _cellStyleCache.Clear();
    }

    private void InvalidateGeometryCache()
    {
        _cachedSheet = null;
        _cellStyleCache.Clear();
    }

    private int GetFrozenColumnCount()
    {
        if (Sheet is null)
            return 0;

        return Math.Clamp(Sheet.FreezeColumnCount, 0, Sheet.ColumnCount);
    }

    private IReadOnlyList<int> GetFrozenColumnIndices()
    {
        var frozenCount = GetFrozenColumnCount();
        if (frozenCount == 0)
            return [];

        var columns = new int[frozenCount];
        for (var col = 0; col < frozenCount; col++)
            columns[col] = col;
        return columns;
    }

    private IReadOnlyList<int> GetScrollableColumnIndices()
    {
        if (Sheet is null)
            return [];

        var frozenCount = GetFrozenColumnCount();
        if (!UseHorizontalVirtualization)
        {
            var fullColumns = new int[Sheet.ColumnCount - frozenCount];
            for (var col = frozenCount; col < Sheet.ColumnCount; col++)
                fullColumns[col - frozenCount] = col;
            return fullColumns;
        }

        var (startCol, endCol) = GetVisibleScrollableColumnRange();
        if (endCol < startCol)
            return [];

        var columns = new int[endCol - startCol + 1];
        for (var col = startCol; col <= endCol; col++)
            columns[col - startCol] = col;
        return columns;
    }

    private (int StartCol, int EndCol) GetVisibleScrollableColumnRange()
    {
        if (Sheet is null)
            return (0, -1);

        var frozenCount = GetFrozenColumnCount();
        if (!UseHorizontalVirtualization)
            return (frozenCount, Sheet.ColumnCount - 1);

        if (frozenCount >= Sheet.ColumnCount)
            return (Sheet.ColumnCount, Sheet.ColumnCount - 1);

        EnsureRenderCaches();

        var frozenWidth = GetCumulativeColumnWidth(frozenCount);
        var viewportWidth = Math.Max(ColumnWidth * 4, _viewportWidth - RowHeaderWidth - frozenWidth);
        var visibleLeft = Math.Max(frozenWidth, _horizontalScrollLeft + frozenWidth);
        var visibleRight = Math.Max(visibleLeft, visibleLeft + viewportWidth);

        var startCol = Math.Max(frozenCount, FindColumnAtOffset(visibleLeft) - HorizontalOverscanColumnCount);
        var endCol = Math.Min(Sheet.ColumnCount - 1, FindColumnAtOffset(visibleRight) + HorizontalOverscanColumnCount);

        return (startCol, Math.Max(startCol, endCol));
    }

    private double GetLeftColumnSpacerWidth()
    {
        if (Sheet is null || !UseHorizontalVirtualization)
            return 0;

        var frozenCount = GetFrozenColumnCount();
        var (startCol, _) = GetVisibleScrollableColumnRange();
        if (startCol <= frozenCount)
            return 0;

        return Math.Max(0, GetCumulativeColumnWidth(startCol) - GetCumulativeColumnWidth(frozenCount));
    }

    private double GetRightColumnSpacerWidth()
    {
        if (Sheet is null || !UseHorizontalVirtualization)
            return 0;

        var (_, endCol) = GetVisibleScrollableColumnRange();
        if (endCol >= Sheet.ColumnCount - 1)
            return 0;

        return Math.Max(0, GetCumulativeColumnWidth(Sheet.ColumnCount) - GetCumulativeColumnWidth(endCol + 1));
    }

    private string GetColumnHeaderSpacerStyle(double width)
    {
        return $"width: {width}px; height: {ColumnHeaderHeight}px;";
    }

    private string GetRowColumnSpacerStyle(double width, int rowIndex)
    {
        return $"width: {width}px; height: {GetRowHeight(rowIndex)}px;";
    }

    private void EnsureColumnInVirtualViewport(int col)
    {
        if (Sheet is null || !UseHorizontalVirtualization || IsFrozenCol(col))
            return;

        var frozenWidth = GetCumulativeColumnWidth(GetFrozenColumnCount());
        var viewportWidth = Math.Max(ColumnWidth * 4, _viewportWidth - RowHeaderWidth - frozenWidth);
        var left = GetCumulativeColumnWidth(col);
        var right = GetCumulativeColumnWidth(col + 1);
        var visibleLeft = _horizontalScrollLeft + frozenWidth;
        var visibleRight = visibleLeft + viewportWidth;

        if (left < visibleLeft)
        {
            _horizontalScrollLeft = Math.Max(0, left - frozenWidth);
        }
        else if (right > visibleRight)
        {
            _horizontalScrollLeft = Math.Max(0, right - viewportWidth - frozenWidth);
        }
    }

    [JSInvokable]
    public Task OnSpreadsheetViewportChanged(double scrollLeft, double clientWidth)
    {
        var nextScrollLeft = Math.Max(0, scrollLeft);
        var nextViewportWidth = clientWidth > 0 ? clientWidth : DefaultHorizontalViewportWidth;
        if (Math.Abs(_horizontalScrollLeft - nextScrollLeft) < 0.5
            && Math.Abs(_viewportWidth - nextViewportWidth) < 0.5)
        {
            return Task.CompletedTask;
        }

        _horizontalScrollLeft = nextScrollLeft;
        _viewportWidth = nextViewportWidth;
        return InvokeAsync(StateHasChanged);
    }

    private int ComputeRowsHash()
    {
        if (Sheet is null) return 0;
        var hash = new HashCode();
        hash.Add(Sheet.Rows.Count);
        foreach (var (index, row) in Sheet.Rows.OrderBy(kv => kv.Key))
        {
            hash.Add(index);
            hash.Add(row.Height);
            hash.Add(row.IsHidden);
        }
        return hash.ToHashCode();
    }

    private int ComputeColumnsHash()
    {
        if (Sheet is null) return 0;
        var hash = new HashCode();
        hash.Add(Sheet.Columns.Count);
        foreach (var (index, column) in Sheet.Columns.OrderBy(kv => kv.Key))
        {
            hash.Add(index);
            hash.Add(column.Width);
            hash.Add(column.IsHidden);
        }
        return hash.ToHashCode();
    }

    private int ComputeMergedCellsHash()
    {
        if (Sheet is null) return 0;
        var hash = new HashCode();
        hash.Add(Sheet.MergedCells.Count);
        foreach (var range in Sheet.MergedCells)
        {
            hash.Add(range.StartRow);
            hash.Add(range.StartCol);
            hash.Add(range.EndRow);
            hash.Add(range.EndCol);
        }
        return hash.ToHashCode();
    }

    private void RebuildGeometryCache()
    {
        if (Sheet is null)
            return;

        _rowHeights = new double[Sheet.RowCount];
        _rowOffsets = new double[Sheet.RowCount + 1];
        for (var row = 0; row < Sheet.RowCount; row++)
        {
            var height = GetConfiguredRowHeight(row);
            _rowHeights[row] = height;
            _rowOffsets[row + 1] = _rowOffsets[row] + height;
        }

        _columnWidths = new double[Sheet.ColumnCount];
        _columnOffsets = new double[Sheet.ColumnCount + 1];
        for (var col = 0; col < Sheet.ColumnCount; col++)
        {
            var width = GetConfiguredColumnWidth(col);
            _columnWidths[col] = width;
            _columnOffsets[col + 1] = _columnOffsets[col] + width;
        }
    }

    private void RebuildColumnLetterCache()
    {
        if (Sheet is null)
            return;

        _columnLetters = new string[Sheet.ColumnCount];
        for (var col = 0; col < Sheet.ColumnCount; col++)
        {
            _columnLetters[col] = SpreadsheetRange.ColumnIndexToLetters(col);
        }
    }

    private void RebuildMergedCellCache()
    {
        _mergedStartLookup.Clear();
        _mergedHiddenLookup.Clear();
        if (Sheet is null)
            return;

        foreach (var range in Sheet.MergedCells)
        {
            _mergedStartLookup[(range.StartRow, range.StartCol)] = range;
            for (var row = range.StartRow; row <= range.EndRow; row++)
            {
                for (var col = range.StartCol; col <= range.EndCol; col++)
                {
                    if (row == range.StartRow && col == range.StartCol)
                        continue;
                    _mergedHiddenLookup.Add((row, col));
                }
            }
        }
    }

    private void EnsureRenderCaches()
    {
        if (!ReferenceEquals(_cachedSheet, Sheet)
            || Sheet is not null && (_rowHeights.Length != Sheet.RowCount || _columnWidths.Length != Sheet.ColumnCount))
        {
            RefreshRenderCachesIfNeeded();
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
        var startRef = SelectionStartRef ?? Sheet?.ActiveCellRef ?? "A1";
        var endRef = SelectionEndRef ?? SelectionStartRef ?? Sheet?.ActiveCellRef ?? "A1";
        var activeRef = Sheet?.ActiveCellRef;
        if (_selectionBoundsCache is { } cached
            && cached.StartRef == startRef
            && cached.EndRef == endRef
            && cached.ActiveRef == activeRef)
        {
            return cached.Bounds;
        }

        var start = ParseCellRef(startRef);
        var end = ParseCellRef(endRef);
        var bounds = (
            Math.Min(start.Row, end.Row),
            Math.Min(start.Col, end.Col),
            Math.Max(start.Row, end.Row),
            Math.Max(start.Col, end.Col)
        );
        _selectionBoundsCache = (startRef, endRef, activeRef, bounds);
        return bounds;
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

    private string GetColumnLetters(int col)
    {
        EnsureRenderCaches();
        if ((uint)col < (uint)_columnLetters.Length)
            return _columnLetters[col];

        return SpreadsheetRange.ColumnIndexToLetters(col);
    }

    private string GetCellRef(int row, int col)
    {
        return $"{GetColumnLetters(col)}{row + 1}";
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
                yield return GetCellRef(r, c);
            }
        }
    }

    private bool IsActiveCell(string cellRef)
    {
        return Sheet?.ActiveCellRef?.Equals(cellRef, StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool IsCellSelected(int row, int col, string cellRef)
    {
        if (Sheet?.ActiveCellRef?.Equals(cellRef, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (!HasRangeSelection)
            return false;

        var bounds = GetSelectionBounds();
        return row >= bounds.StartRow && row <= bounds.EndRow && col >= bounds.StartCol && col <= bounds.EndCol;
    }

    private string GetCellClass(bool isActive, bool isSelected, bool isMergedHidden, string cellRef)
    {
        var classes = new System.Text.StringBuilder();
        if (isActive) classes.Append(" tm-spreadsheet-cell--active");
        if (isSelected && !IsInFormulaPointMode) classes.Append(" tm-spreadsheet-cell--selected");
        if (isMergedHidden) classes.Append(" tm-spreadsheet-cell--merged-hidden");
        var colorIdx = GetFormulaRefColorIndex(cellRef);
        if (colorIdx >= 0) classes.Append($" tm-spreadsheet-cell--formula-ref-{colorIdx}");
        return classes.ToString().Trim();
    }

    private SpreadsheetRange? GetMergedRangeStart(int row, int col)
    {
        EnsureRenderCaches();
        return _mergedStartLookup.TryGetValue((row, col), out var range) ? range : null;
    }

    private SpreadsheetRange? GetMergedRangeCovering(int row, int col)
    {
        EnsureRenderCaches();
        if (!_mergedHiddenLookup.Contains((row, col)) || Sheet?.MergedCells is null)
            return null;

        foreach (var range in Sheet.MergedCells)
        {
            if (range.Contains(row, col) && !(range.StartRow == row && range.StartCol == col))
                return range;
        }
        return null;
    }

    private bool IsCellMergedAndHidden(int row, int col)
    {
        EnsureRenderCaches();
        return _mergedHiddenLookup.Contains((row, col));
    }

    private bool IsSelectionEndCell(int row, int col)
    {
        if (!HasRangeSelection) return Sheet?.ActiveCellRef == GetCellRef(row, col);
        var bounds = GetSelectionBounds();
        return row == bounds.EndRow && col == bounds.EndCol;
    }

    private string GetRowHeaderClass(int rowIndex)
    {
        var classes = new System.Text.StringBuilder();
        if (HasRangeSelection)
        {
            var bounds = GetSelectionBounds();
            if (rowIndex >= bounds.StartRow && rowIndex <= bounds.EndRow)
                classes.Append("tm-spreadsheet-header-cell--selected ");
        }
        if (rowIndex > 0 && IsRowHidden(rowIndex - 1))
            classes.Append("tm-spreadsheet-header-cell--hidden-above ");
        return classes.ToString().Trim();
    }

    private string GetColumnHeaderClass(int colIndex)
    {
        var classes = new System.Text.StringBuilder();
        if (HasRangeSelection)
        {
            var bounds = GetSelectionBounds();
            if (colIndex >= bounds.StartCol && colIndex <= bounds.EndCol)
                classes.Append("tm-spreadsheet-header-cell--selected ");
        }
        if (colIndex > 0 && IsColumnHidden(colIndex - 1))
            classes.Append("tm-spreadsheet-header-cell--hidden-before ");
        return classes.ToString().Trim();
    }

    private bool IsRowHidden(int rowIndex) =>
        Sheet?.Rows.TryGetValue(rowIndex, out var r) == true && r.IsHidden;

    private bool IsColumnHidden(int colIndex) =>
        Sheet?.Columns.TryGetValue(colIndex, out var c) == true && c.IsHidden;

    private double GetRowHeight(int rowIndex)
    {
        if (_isResizingRow && rowIndex == _resizingRowIndex)
            return _resizePreviewHeight;

        EnsureRenderCaches();
        return (uint)rowIndex < (uint)_rowHeights.Length
            ? _rowHeights[rowIndex]
            : GetConfiguredRowHeight(rowIndex);
    }

    private double GetColumnWidth(int colIndex)
    {
        if (_isResizingCol && colIndex == _resizingColIndex)
            return _resizePreviewWidth;

        EnsureRenderCaches();
        return (uint)colIndex < (uint)_columnWidths.Length
            ? _columnWidths[colIndex]
            : GetConfiguredColumnWidth(colIndex);
    }

    private double GetConfiguredRowHeight(int rowIndex)
    {
        if (IsRowHidden(rowIndex)) return 0;
        if (Sheet?.Rows.TryGetValue(rowIndex, out var row) == true && row.Height.HasValue)
            return row.Height.Value;
        return RowHeight;
    }

    private double GetConfiguredColumnWidth(int colIndex)
    {
        if (IsColumnHidden(colIndex)) return 0;
        if (Sheet?.Columns.TryGetValue(colIndex, out var col) == true && col.Width.HasValue)
            return col.Width.Value;
        return ColumnWidth;
    }

    private string GetGridCursorStyle()
    {
        if (_isResizingCol) return "cursor: col-resize;";
        if (_isResizingRow) return "cursor: row-resize;";
        if (IsFormatPainterActive) return "cursor: crosshair;";
        return string.Empty;
    }

    private double GetCumulativeRowHeight(int upToRow)
    {
        if (_isResizingRow)
        {
            var sum = 0.0;
            var rowCount = Sheet?.RowCount ?? 0;
            for (var row = 0; row < upToRow && row < rowCount; row++)
                sum += GetRowHeight(row);
            return sum;
        }

        EnsureRenderCaches();
        if (_rowOffsets.Length == 0)
            return 0;

        var index = Math.Clamp(upToRow, 0, _rowOffsets.Length - 1);
        return _rowOffsets[index];
    }

    private double GetCumulativeColumnWidth(int upToCol)
    {
        if (_isResizingCol)
        {
            var sum = 0.0;
            var columnCount = Sheet?.ColumnCount ?? 0;
            for (var col = 0; col < upToCol && col < columnCount; col++)
                sum += GetColumnWidth(col);
            return sum;
        }

        EnsureRenderCaches();
        if (_columnOffsets.Length == 0)
            return 0;

        var index = Math.Clamp(upToCol, 0, _columnOffsets.Length - 1);
        return _columnOffsets[index];
    }

    private bool IsFrozenRow(int rowIndex) => Sheet?.FreezeRowCount > 0 && rowIndex < Sheet.FreezeRowCount;
    private bool IsFrozenCol(int colIndex) => Sheet?.FreezeColumnCount > 0 && colIndex < Sheet.FreezeColumnCount;

    private string GetFreezeCellStyle(int rowIndex, int colIndex)
    {
        var sb = new System.Text.StringBuilder();
        if (IsFrozenRow(rowIndex))
        {
            var top = ColumnHeaderHeight + GetCumulativeRowHeight(rowIndex);
            sb.Append($"position: sticky; top: {top}px;");
        }
        if (IsFrozenCol(colIndex))
        {
            var left = RowHeaderWidth + GetCumulativeColumnWidth(colIndex);
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
        var left = RowHeaderWidth + GetCumulativeColumnWidth(colIndex);
        return $" position: sticky; left: {left}px; z-index: 3;";
    }

    private string GetRowHeaderFreezeStyle(int rowIndex)
    {
        if (!IsFrozenRow(rowIndex)) return string.Empty;
        var top = ColumnHeaderHeight + GetCumulativeRowHeight(rowIndex);
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
        _cellStyleCache.Clear();
    }

    /// <summary>Builds the inline CSS style string for a cell including dimensions, font, colors, alignment and borders.</summary>
    private string GetCellStyleString(SpreadsheetCell? cell, int colIndex, int rowIndex)
    {
        var style = cell?.Style;
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

        var freezeStyle = GetFreezeCellStyle(rowIndex, colIndex);
        if (style is null)
        {
            return string.IsNullOrEmpty(freezeStyle)
                ? $"width: {width}px; height: {height}px;"
                : $"width: {width}px; height: {height}px; {freezeStyle}";
        }

        var cacheKey = BuildCellStyleCacheKey(cell, style, colIndex, rowIndex, width, height, freezeStyle);
        if (_cellStyleCache.TryGetValue(cacheKey, out var cachedStyle))
            return cachedStyle;

        var sb = new System.Text.StringBuilder();
        sb.Append($"width: {width}px; height: {height}px;");
        if (!string.IsNullOrEmpty(freezeStyle))
            sb.Append($" {freezeStyle}");

        // Font
        sb.Append($" font-family: {style.FontFamily}; font-size: {style.FontSize}pt;");
        if (style.Bold) sb.Append(" font-weight: bold;");
        if (style.Italic) sb.Append(" font-style: italic;");
        if (style.Underline || style.DoubleUnderline || style.StrikeThrough)
        {
            var decorations = new System.Text.StringBuilder();
            if (style.DoubleUnderline) decorations.Append(" underline");
            else if (style.Underline) decorations.Append(" underline");
            if (style.StrikeThrough) decorations.Append(" line-through");
            sb.Append($" text-decoration:{decorations};");
            if (style.DoubleUnderline) sb.Append(" text-decoration-style: double;");
        }

        // Colors
        if (!string.IsNullOrEmpty(style.ForeColor) && style.ForeColor != "#000000")
            sb.Append($" color: {style.ForeColor};");
        if (!string.IsNullOrEmpty(style.BackgroundColor) && style.BackgroundColor != "transparent")
            sb.Append($" background-color: {style.BackgroundColor};");

        // Alignment
        sb.Append($" justify-content: {GetJustifyContent(GetEffectiveHAlign(style, cell))};");
        sb.Append($" align-items: {GetAlignItems(style.VerticalAlign)};");
        if (style.TextWrap)
            sb.Append(" white-space: normal; word-break: break-word;");
        if (style.Indent > 0)
            sb.Append($" padding-left: {style.Indent * 12}px;");
        if (style.TextRotation != 0)
        {
            if (style.TextRotation == 90)
                sb.Append(" writing-mode: vertical-rl; transform: rotate(180deg);");
            else if (style.TextRotation == -90)
                sb.Append(" writing-mode: vertical-lr;");
            else
                sb.Append($" transform: rotate({-style.TextRotation}deg);");
        }

        // Borders
        AppendBorderStyle(sb, "border-top", style.BorderTop);
        AppendBorderStyle(sb, "border-right", style.BorderRight);
        AppendBorderStyle(sb, "border-bottom", style.BorderBottom);
        AppendBorderStyle(sb, "border-left", style.BorderLeft);

        var styleString = sb.ToString();
        _cellStyleCache[cacheKey] = styleString;
        return styleString;
    }

    private static string BuildCellStyleCacheKey(
        SpreadsheetCell? cell,
        SpreadsheetCellStyle style,
        int colIndex,
        int rowIndex,
        double width,
        double height,
        string freezeStyle)
    {
        return string.Join('|',
            rowIndex,
            colIndex,
            width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeHelpers.GetHashCode(style),
            cell?.Value?.GetType().FullName ?? string.Empty,
            freezeStyle,
            BuildStyleSignature(style));
    }

    private static string BuildStyleSignature(SpreadsheetCellStyle style)
    {
        return string.Join('|',
            style.FontFamily,
            style.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            style.Bold,
            style.Italic,
            style.Underline,
            style.DoubleUnderline,
            style.StrikeThrough,
            style.Indent,
            style.TextRotation,
            style.ShrinkToFit,
            style.ForeColor,
            style.BackgroundColor,
            style.HorizontalAlign,
            style.VerticalAlign,
            style.TextWrap,
            style.NumberFormat,
            BuildBorderSignature(style.BorderTop),
            BuildBorderSignature(style.BorderRight),
            BuildBorderSignature(style.BorderBottom),
            BuildBorderSignature(style.BorderLeft));
    }

    private static string BuildBorderSignature(SpreadsheetBorder border)
    {
        return $"{border.Style}:{border.Color}";
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

    private static Enums.SpreadsheetHorizontalAlign GetEffectiveHAlign(SpreadsheetCellStyle? style, SpreadsheetCell? cell)
    {
        var align = style?.HorizontalAlign ?? Enums.SpreadsheetHorizontalAlign.General;
        if (align != Enums.SpreadsheetHorizontalAlign.General)
            return align;
        return cell?.Value switch
        {
            double => Enums.SpreadsheetHorizontalAlign.Right,
            bool => Enums.SpreadsheetHorizontalAlign.Center,
            _ => Enums.SpreadsheetHorizontalAlign.Left
        };
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

        if (IsFormatPainterActive)
        {
            Sheet!.ActiveCellRef = cellRef;
            SelectionStartRef = cellRef;
            SelectionEndRef = cellRef;
            ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
            ScheduleCellVisibility(cellRef);
            OnFormatPainterApply.InvokeAsync(cellRef);
            _shouldFocusAfterRender = true;
            return;
        }

        if (IsEditing && IsActiveCell(cellRef))
            return;

        if (IsEditing)
        {
            // Formula-point mode: reference insertion is handled by onmousedown / onmouseenter
            if (IsInFormulaPointMode) return;
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
        ScheduleCellVisibility(cellRef);
        _shouldFocusAfterRender = true;
    }

    private void OnContextMenu(MouseEventArgs e)
    {
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        var (row, col) = ParseCellRef(Sheet?.ActiveCellRef ?? "A1");
        _contextMenuColIndex = col;
        _contextMenuRowIndex = row;
    }

    private void CloseContextMenu()
    {
        _contextMenuVisible = false;
    }

    private void ContextMenuFormatCells()
    {
        CloseContextMenu();
        OnFormatCellsRequested.InvokeAsync();
    }

    private void ContextMenuActivateFormatPainter()
    {
        CloseContextMenu();
        OnFormatPainterActivateRequested.InvokeAsync();
    }

    private void ContextMenuClearFormatting()
    {
        CloseContextMenu();
        OnClearFormattingRequested.InvokeAsync();
    }

    private void ContextMenuClearContent()
    {
        CloseContextMenu();
        OnClearContentRequested.InvokeAsync();
    }

    private void ContextMenuClearAll()
    {
        CloseContextMenu();
        OnClearAllRequested.InvokeAsync();
    }

    private void ContextMenuHideRow()
    {
        CloseContextMenu();
        var bounds = GetSelectionBounds();
        OnHideRowsRequested.InvokeAsync((bounds.StartRow, bounds.EndRow));
    }

    private void ContextMenuUnhideRows()
    {
        CloseContextMenu();
        var bounds = GetSelectionBounds();
        OnUnhideRowsRequested.InvokeAsync((Math.Max(0, bounds.StartRow - 1), bounds.EndRow + 1));
    }

    private void ContextMenuHideColumn()
    {
        CloseContextMenu();
        var bounds = GetSelectionBounds();
        OnHideColumnsRequested.InvokeAsync((bounds.StartCol, bounds.EndCol));
    }

    private void ContextMenuUnhideColumns()
    {
        CloseContextMenu();
        var bounds = GetSelectionBounds();
        OnUnhideColumnsRequested.InvokeAsync((Math.Max(0, bounds.StartCol - 1), bounds.EndCol + 1));
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

    private void ContextMenuSetColumnWidth()
    {
        CloseContextMenu();
        _colWidthInputValue = ((int)Math.Round(GetColumnWidth(_contextMenuColIndex))).ToString();
        _showColWidthDialog = true;
    }

    private void ContextMenuSetRowHeight()
    {
        CloseContextMenu();
        _rowHeightInputValue = ((int)Math.Round(GetRowHeight(_contextMenuRowIndex))).ToString();
        _showRowHeightDialog = true;
    }

    private void ApplyColWidth()
    {
        if (double.TryParse(_colWidthInputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var width) && width >= 16)
        {
            InvalidateGeometryCache();
            OnColumnResizeRequested.InvokeAsync((_contextMenuColIndex, width));
        }
        _showColWidthDialog = false;
    }

    private void ApplyRowHeight()
    {
        if (double.TryParse(_rowHeightInputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var height) && height >= 8)
        {
            InvalidateGeometryCache();
            OnRowResizeRequested.InvokeAsync((_contextMenuRowIndex, height));
        }
        _showRowHeightDialog = false;
    }

    private void OnColWidthInputKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") ApplyColWidth();
        else if (e.Key == "Escape") _showColWidthDialog = false;
    }

    private void OnRowHeightInputKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") ApplyRowHeight();
        else if (e.Key == "Escape") _showRowHeightDialog = false;
    }

    private void OnColResizerMouseDown(MouseEventArgs e, int colIndex)
    {
        _isResizingCol = true;
        _resizingColIndex = colIndex;
        _resizeStartX = e.ClientX;
        _resizeStartWidth = GetColumnWidth(colIndex);
        _resizePreviewWidth = _resizeStartWidth;
    }

    private void OnRowResizerMouseDown(MouseEventArgs e, int rowIndex)
    {
        _isResizingRow = true;
        _resizingRowIndex = rowIndex;
        _resizeStartY = e.ClientY;
        _resizeStartHeight = GetRowHeight(rowIndex);
        _resizePreviewHeight = _resizeStartHeight;
    }

    private void OnColResizerDoubleClick(int colIndex)
    {
        if (Sheet is null) return;
        var maxLen = 0;
        for (var r = 0; r < Sheet.RowCount; r++)
        {
            var cellRef = GetCellRef(r, colIndex);
            if (Sheet.Cells.TryGetValue(cellRef, out var cell))
            {
                var text = cell.DisplayValue ?? cell.Value?.ToString() ?? string.Empty;
                if (text.Length > maxLen) maxLen = text.Length;
            }
        }
        var autoWidth = Math.Max(40.0, maxLen * 7.5 + 16);
        InvalidateGeometryCache();
        OnColumnResizeRequested.InvokeAsync((colIndex, autoWidth));
    }

    private void OnAutoFillMouseDown(MouseEventArgs e)
    {
        if (Sheet is null) return;
        _isAutoFillDragging = true;
        var bounds = GetSelectionBounds();
        _autoFillSourceRange = $"{GetCellRef(bounds.StartRow, bounds.StartCol)}:{GetCellRef(bounds.EndRow, bounds.EndCol)}";
        _autoFillPreviewRange = _autoFillSourceRange;
    }

    private void OnAutoFillMouseMove(MouseEventArgs e)
    {
        if (_isResizingCol)
        {
            var delta = e.ClientX - _resizeStartX;
            _resizePreviewWidth = Math.Max(16, _resizeStartWidth + delta);
            StateHasChanged();
            return;
        }

        if (_isResizingRow)
        {
            var delta = e.ClientY - _resizeStartY;
            _resizePreviewHeight = Math.Max(8, _resizeStartHeight + delta);
            StateHasChanged();
            return;
        }

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
            _autoFillPreviewRange = $"{GetCellRef(startRow, startCol)}:{GetCellRef(endRow, startCol)}";
        }
        else
        {
            _autoFillPreviewRange = $"{GetCellRef(startRow, startCol)}:{GetCellRef(startRow, endCol)}";
        }
        StateHasChanged();
    }

    private void OnAutoFillMouseUp(MouseEventArgs e)
    {
        if (_isResizingCol)
        {
            var finalWidth = Math.Max(16, _resizePreviewWidth);
            _isResizingCol = false;
            InvalidateGeometryCache();
            OnColumnResizeRequested.InvokeAsync((_resizingColIndex, finalWidth));
            StateHasChanged();
            return;
        }

        if (_isResizingRow)
        {
            var finalHeight = Math.Max(8, _resizePreviewHeight);
            _isResizingRow = false;
            InvalidateGeometryCache();
            OnRowResizeRequested.InvokeAsync((_resizingRowIndex, finalHeight));
            StateHasChanged();
            return;
        }

        if (_isFormulaPointDragging)
        {
            _isFormulaPointDragging = false;
            _formulaPointDragAnchor = null;
            _formulaPointDragCurrent = null;
            _shouldFocusAfterRender = true;
            return;
        }

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
        var headerHeight = ColumnHeaderHeight;
        var rowHeaderWidth = RowHeaderWidth;
        var y = clientY - headerHeight;
        var x = clientX - rowHeaderWidth;
        if (y < 0 || x < 0) return (-1, -1);

        var row = FindRowAtOffset(y);
        var col = FindColumnAtOffset(x);

        return (Math.Min(row, Sheet.RowCount - 1), Math.Min(col, Sheet.ColumnCount - 1));
    }

    private int FindRowAtOffset(double offset)
    {
        EnsureRenderCaches();
        return FindIndexAtOffset(_rowOffsets, offset);
    }

    private int FindColumnAtOffset(double offset)
    {
        EnsureRenderCaches();
        return FindIndexAtOffset(_columnOffsets, offset);
    }

    private static int FindIndexAtOffset(double[] offsets, double offset)
    {
        if (offsets.Length <= 1)
            return 0;

        var index = Array.BinarySearch(offsets, offset);
        if (index >= 0)
            return Math.Clamp(index, 0, offsets.Length - 2);

        index = ~index - 1;
        return Math.Clamp(index, 0, offsets.Length - 2);
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
        ScheduleCellVisibility(rowIndex, 0);
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
        ScheduleCellVisibility(0, colIndex);
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
        ScheduleCellVisibility(0, 0);
    }

    private void StartEdit(string cellRef)
    {
        StartEdit(cellRef, null);
    }

    private void StartEdit(string cellRef, string? initialValue)
    {
        var sheet = Sheet;
        if (sheet is null)
            return;

        if (!string.Equals(sheet.ActiveCellRef, cellRef, StringComparison.OrdinalIgnoreCase))
        {
            sheet.ActiveCellRef = cellRef;
            SelectionStartRef = cellRef;
            SelectionEndRef = cellRef;
            ActiveCellChanged.InvokeAsync(sheet.ActiveCellRef);
            ScheduleCellVisibility(cellRef);
        }

        IsEditing = true;
        _editValue = initialValue;

        // If no initial value provided, load the cell's formula or value so that
        // existing formulas immediately enter formula-point mode and highlight refs.
        if (_editValue is null && sheet.Cells.TryGetValue(cellRef, out var cell))
        {
            _editValue = cell.Formula ?? cell.Value?.ToString() ?? string.Empty;
        }

        if (_editValue?.StartsWith("=") == true)
        {
            RefreshFormulaRefColors();
        }

        _shouldFocusAfterRender = true;
        OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(sheet, sheet.ActiveCellRef!, true));
    }

    /// <summary>Appends text to the current edit value. Used for formula cell reference insertion.</summary>
    public void AppendEditValue(string text)
    {
        _editValue = (_editValue ?? string.Empty) + text;
        _shouldFocusAfterRender = true;
        StateHasChanged();
    }

    /// <summary>Inserts or replaces the last cell reference in the formula (formula-point mode).</summary>
    public void InsertCellRefIntoFormula(string cellRef)
    {
        _editValue = FormulaReferenceAdjuster.InsertOrReplaceLastRef(_editValue ?? "=", cellRef);
        RefreshFormulaRefColors();
        _shouldFocusAfterRender = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public void InvalidateRenderedCells(IEnumerable<string> cellRefs)
    {
    }

    /// <inheritdoc />
    public void InvalidateRenderedRows(IEnumerable<int> rowIndices)
    {
    }

    /// <inheritdoc />
    public void InvalidateRenderedColumns(IEnumerable<int> columnIndices)
    {
    }

    /// <inheritdoc />
    public void ClearRenderedCache()
    {
    }

    private void OnEditInput(ChangeEventArgs e)
    {
        _editValue = e.Value?.ToString();
        RefreshFormulaRefColors();
    }

    /// <summary>
    /// Blur handler for the edit input. In formula-point mode a click on another cell
    /// sets <see cref="_suppressNextBlurCommit"/> in <see cref="OnCellMouseDown"/> so that
    /// the edit survives the focus loss and can continue after the reference is inserted.
    /// </summary>
    private void OnEditBlur(FocusEventArgs e)
    {
        if (_suppressNextBlurCommit)
        {
            _suppressNextBlurCommit = false;
            _shouldFocusAfterRender = true;
            StateHasChanged();
            return;
        }
        CommitEdit();
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
            case "F4":
                if (_editValue?.StartsWith("=") == true)
                {
                    _editValue = FormulaReferenceAdjuster.CycleLastAbsoluteRef(_editValue);
                    StateHasChanged();
                }
                break;
        }
    }

    private void CommitEdit()
    {
        if (!IsEditing) return;
        IsEditing = false;
        _formulaRefColors.Clear();
        _formulaRangeColors.Clear();
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
        _formulaRefColors.Clear();
        _formulaRangeColors.Clear();
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
                case "1":
                    OnFormatCellsRequested.InvokeAsync();
                    return;
                case "5":
                    OnStrikeThroughToggleRequested.InvokeAsync();
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
                if (IsFormatPainterActive)
                {
                    OnFormatPainterCancel.InvokeAsync();
                    return;
                }
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
        ScheduleCellVisibility(row, col);
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

    private void ScheduleCellVisibility(string cellRef)
    {
        var (row, col) = ParseCellRef(cellRef);
        ScheduleCellVisibility(row, col);
    }

    private void ScheduleCellVisibility(int row, int col)
    {
        if (Sheet is null) return;
        _pendingVisibleCell = (
            Math.Clamp(row, 0, Sheet.RowCount - 1),
            Math.Clamp(col, 0, Sheet.ColumnCount - 1));
        EnsureColumnInVirtualViewport(_pendingVisibleCell.Value.Col);
    }

    private CellVisibilityRequest BuildCellVisibilityRequest(int row, int col)
    {
        var left = RowHeaderWidth + GetCumulativeColumnWidth(col);
        var top = ColumnHeaderHeight + GetCumulativeRowHeight(row);
        var width = GetColumnWidth(col);
        var height = GetRowHeight(row);

        return new CellVisibilityRequest(
            Left: left,
            Top: top,
            Right: left + width,
            Bottom: top + height,
            Width: width,
            Height: height,
            FrozenRow: IsFrozenRow(row),
            FrozenColumn: IsFrozenCol(col));
    }

    private async Task EnsurePendingCellVisibleAsync()
    {
        if (_pendingVisibleCell is not { } pending || Sheet is null)
            return;

        _pendingVisibleCell = null;
        var request = BuildCellVisibilityRequest(pending.Row, pending.Col);

        try
        {
            await JS.InvokeVoidAsync(
                "tmSpreadsheetGrid.ensureCellVisible",
                _gridElement,
                request,
                new { RowHeaderWidth, ColumnHeaderHeight });
        }
        catch (JSException)
        {
            // The helper script may be missing in a consuming app; keyboard navigation must still work.
        }
        catch (InvalidOperationException)
        {
            // JS interop is unavailable during prerender/static rendering.
        }
    }

    // ── Formula-point mode helpers ───────────────────────────────────────────

    private void RefreshFormulaRefColors()
    {
        _formulaRefColors.Clear();
        _formulaRangeColors.Clear();
        if (!IsInFormulaPointMode || string.IsNullOrEmpty(_editValue)) return;
        var refs = FormulaReferenceAdjuster.ParseFormulaReferences(_editValue);
        for (int i = 0; i < refs.Count; i++)
        {
            var raw = refs[i];
            var colorIdx = i % FormulaRefColorCount;
            if (raw.Contains(':'))
            {
                try
                {
                    var range = SpreadsheetRange.Parse(raw);
                    _formulaRangeColors.Add((range.StartRow, range.StartCol, range.EndRow, range.EndCol, colorIdx));
                }
                catch { }
            }
            else
            {
                _formulaRefColors[raw.Replace("$", "").ToUpperInvariant()] = colorIdx;
            }
        }
    }

    private int GetFormulaRefColorIndex(string cellRef)
    {
        if (_formulaRefColors.Count == 0 && _formulaRangeColors.Count == 0) return -1;
        if (_formulaRefColors.TryGetValue(cellRef.Replace("$", "").ToUpperInvariant(), out var idx)) return idx;
        var (row, col) = ParseCellRef(cellRef);
        foreach (var (sr, sc, er, ec, ci) in _formulaRangeColors)
        {
            if (row >= sr && row <= er && col >= sc && col <= ec) return ci;
        }
        return -1;
    }

    private void OnCellMouseDown(string cellRef, MouseEventArgs e)
    {
        if (!IsInFormulaPointMode) return;
        if (IsActiveCell(cellRef)) return;
        _suppressNextBlurCommit = true;
        _isFormulaPointDragging = true;
        _formulaPointDragAnchor = cellRef;
        _formulaPointDragCurrent = cellRef;
        _ = OnCellReferenceRequested.InvokeAsync(cellRef);
    }

    private void OnCellMouseEnter(string cellRef)
    {
        if (!_isFormulaPointDragging || _formulaPointDragAnchor is null) return;
        if (cellRef == _formulaPointDragCurrent) return;
        _formulaPointDragCurrent = cellRef;
        var rangeRef = BuildFormulaPointRange(_formulaPointDragAnchor, cellRef);
        _editValue = FormulaReferenceAdjuster.InsertOrReplaceLastRef(_editValue ?? "=", rangeRef);
        RefreshFormulaRefColors();
        StateHasChanged();
    }

    private static string BuildFormulaPointRange(string anchor, string current)
    {
        var (ar, ac) = ParseCellRef(anchor);
        var (cr, cc) = ParseCellRef(current);
        var sr = Math.Min(ar, cr);
        var er = Math.Max(ar, cr);
        var sc = Math.Min(ac, cc);
        var ec = Math.Max(ac, cc);
        if (sr == er && sc == ec) return anchor;
        return $"{ToCellRef(sr, sc)}:{ToCellRef(er, ec)}";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RegisterViewportObserverAsync();
        }

        await EnsurePendingCellVisibleAsync();

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

    private async Task RegisterViewportObserverAsync()
    {
        if (_viewportObserverRegistered)
            return;

        _dotNetRef ??= DotNetObjectReference.Create(this);
        try
        {
            await JS.InvokeVoidAsync("tmSpreadsheetGrid.observeViewport", _gridElement, _dotNetRef);
            _viewportObserverRegistered = true;
        }
        catch (JSException)
        {
            // The helper script may be missing in a consuming app; the grid still renders with the fallback viewport.
        }
        catch (InvalidOperationException)
        {
            // JS interop is unavailable during prerender/static rendering.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_viewportObserverRegistered)
        {
            try
            {
                await JS.InvokeVoidAsync("tmSpreadsheetGrid.disposeViewportObserver", _gridElement);
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        _dotNetRef?.Dispose();
    }

    private sealed record CellVisibilityRequest(
        double Left,
        double Top,
        double Right,
        double Bottom,
        double Width,
        double Height,
        bool FrozenRow,
        bool FrozenColumn);
}
