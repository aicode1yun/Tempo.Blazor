using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Rendering;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Renders a spreadsheet sheet using a hybrid canvas surface with HTML editing and accessibility overlays.
/// </summary>
public partial class TmSpreadsheetCanvasGrid : IAsyncDisposable, ISpreadsheetGridController
{
    private const int RowOverscanCount = 32;
    private const int ColumnOverscanCount = 10;
    private readonly SpreadsheetGridGeometry _geometry = new();
    private readonly SpreadsheetSelectionState _selection = new();
    private readonly Dictionary<(int Row, int Col), SpreadsheetRange> _mergedStartLookup = [];
    private readonly HashSet<(int Row, int Col)> _mergedHiddenLookup = [];
    private ElementReference _rootElement;
    private ElementReference _canvasElement;
    private ElementReference _editInput;
    private DotNetObjectReference<TmSpreadsheetCanvasGrid>? _dotNetRef;
    private SpreadsheetSheet? _geometrySheet;
    private SpreadsheetViewportState _viewport = SpreadsheetViewportState.Default;
    private double _geometryRowHeight;
    private double _geometryColumnWidth;
    private int _geometryRowCount;
    private int _geometryColumnCount;
    private int _geometryRowsMetadataCount;
    private int _geometryColumnsMetadataCount;
    private int _geometryMergedCount;
    private int _geometryFreezeRowCount;
    private int _geometryFreezeColumnCount;
    private bool _registered;
    private bool _needsRender = true;
    private bool _shouldFocusAfterRender;
    private bool _shouldFocusEditorAfterRender;
    private string? _editValue;
    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;
    private int _contextMenuRowIndex;
    private int _contextMenuColIndex;
    private bool _showColWidthDialog;
    private bool _showRowHeightDialog;
    private string _colWidthInputValue = string.Empty;
    private string _rowHeightInputValue = string.Empty;

    [Inject] private IJSRuntime JS { get; set; } = default!;

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

    /// <summary>Called when the user requests a copy operation.</summary>
    [Parameter] public EventCallback OnCopyRequested { get; set; }

    /// <summary>Called when the user requests a paste operation.</summary>
    [Parameter] public EventCallback OnPasteRequested { get; set; }

    /// <summary>Called when the user requests a cut operation.</summary>
    [Parameter] public EventCallback OnCutRequested { get; set; }

    /// <summary>Called when the user requests insert row.</summary>
    [Parameter] public EventCallback OnInsertRowRequested { get; set; }

    /// <summary>Called when the user requests delete row.</summary>
    [Parameter] public EventCallback OnDeleteRowRequested { get; set; }

    /// <summary>Called when the user requests insert column.</summary>
    [Parameter] public EventCallback OnInsertColumnRequested { get; set; }

    /// <summary>Called when the user requests delete column.</summary>
    [Parameter] public EventCallback OnDeleteColumnRequested { get; set; }

    /// <summary>Called when the user requests delete selection.</summary>
    [Parameter] public EventCallback OnDeleteRequested { get; set; }

    /// <summary>Called when the user requests undo.</summary>
    [Parameter] public EventCallback OnUndoRequested { get; set; }

    /// <summary>Called when the user requests redo.</summary>
    [Parameter] public EventCallback OnRedoRequested { get; set; }

    /// <summary>Called when the user requests bold toggle.</summary>
    [Parameter] public EventCallback OnBoldToggleRequested { get; set; }

    /// <summary>Called when the user requests italic toggle.</summary>
    [Parameter] public EventCallback OnItalicToggleRequested { get; set; }

    /// <summary>Called when the user requests underline toggle.</summary>
    [Parameter] public EventCallback OnUnderlineToggleRequested { get; set; }

    /// <summary>Called when the user requests select all.</summary>
    [Parameter] public EventCallback OnSelectAllRequested { get; set; }

    /// <summary>Called when a cell enters or exits edit mode.</summary>
    [Parameter] public EventCallback<SpreadsheetCellEditEventArgs> OnCellEdit { get; set; }

    /// <summary>Called when formula point mode inserts a cell reference.</summary>
    [Parameter] public EventCallback<string> OnCellReferenceRequested { get; set; }

    /// <summary>Called when a column is resized.</summary>
    [Parameter] public EventCallback<(int ColIndex, double Width)> OnColumnResizeRequested { get; set; }

    /// <summary>Called when a row is resized.</summary>
    [Parameter] public EventCallback<(int RowIndex, double Height)> OnRowResizeRequested { get; set; }

    /// <summary>Called when the user requests the Format Cells dialog.</summary>
    [Parameter] public EventCallback OnFormatCellsRequested { get; set; }

    /// <summary>Called when the user requests strikethrough toggle.</summary>
    [Parameter] public EventCallback OnStrikeThroughToggleRequested { get; set; }

    /// <summary>Whether Format Painter mode is active.</summary>
    [Parameter] public bool IsFormatPainterActive { get; set; }

    /// <summary>Called when Format Painter applies to a cell.</summary>
    [Parameter] public EventCallback<string> OnFormatPainterApply { get; set; }

    /// <summary>Called when Format Painter is cancelled.</summary>
    [Parameter] public EventCallback OnFormatPainterCancel { get; set; }

    /// <summary>Called when selected rows should be hidden.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnHideRowsRequested { get; set; }

    /// <summary>Called when hidden rows near the selection should be shown.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnUnhideRowsRequested { get; set; }

    /// <summary>Called when selected columns should be hidden.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnHideColumnsRequested { get; set; }

    /// <summary>Called when hidden columns near the selection should be shown.</summary>
    [Parameter] public EventCallback<(int Start, int End)> OnUnhideColumnsRequested { get; set; }

    /// <summary>Called when Format Painter is activated from context menu.</summary>
    [Parameter] public EventCallback OnFormatPainterActivateRequested { get; set; }

    /// <summary>Called when formatting should be cleared.</summary>
    [Parameter] public EventCallback OnClearFormattingRequested { get; set; }

    /// <summary>Called when content should be cleared.</summary>
    [Parameter] public EventCallback OnClearContentRequested { get; set; }

    /// <summary>Called when content and formatting should be cleared.</summary>
    [Parameter] public EventCallback OnClearAllRequested { get; set; }

    /// <summary>Whether a cell is currently being edited.</summary>
    public bool IsEditing { get; private set; }

    /// <inheritdoc />
    public string? SelectionStartRef => _selection.SelectionStartRef;

    /// <inheritdoc />
    public string? SelectionEndRef => _selection.SelectionEndRef;

    /// <inheritdoc />
    public bool IsInFormulaPointMode => IsEditing && _editValue?.StartsWith("=") == true;

    /// <inheritdoc />
    public string? CurrentEditValue => _editValue;

    private string ActiveCellDomId => $"tm-spreadsheet-canvas-active-{GetHashCode():x}";
    private string LiveRegionText => $"{Sheet?.ActiveCellRef ?? "A1"} {GetActiveCellDisplayValue()}";
    private string TotalScrollableWidthPx => $"{_geometry.ContentWidth + SpreadsheetGridConstants.RowHeaderWidth}px";
    private string TotalScrollableHeightPx => $"{_geometry.ContentHeight + SpreadsheetGridConstants.ColumnHeaderHeight}px";
    private readonly record struct CanvasRowFrame(int Index, double Top, double Height, bool Frozen);
    private readonly record struct CanvasColumnFrame(int Index, double Left, double Width, string Label, bool Frozen);

    protected override void OnParametersSet()
    {
        var structureChanged = HasStructureChanged();
        if (structureChanged)
        {
            _geometry.Update(Sheet, RowHeight, ColumnWidth);
            RebuildMergedCellCache();
            CaptureStructureSignature();
            _needsRender = true;
        }

        if (Sheet is not null)
        {
            Sheet.ActiveCellRef ??= "A1";
            _selection.ActiveCellRef = Sheet.ActiveCellRef;
            _selection.SelectionStartRef ??= Sheet.ActiveCellRef;
            _selection.SelectionEndRef ??= Sheet.ActiveCellRef;
        }
    }

    private bool HasStructureChanged()
    {
        if (!ReferenceEquals(_geometrySheet, Sheet))
            return true;

        if (Sheet is null)
            return false;

        return Math.Abs(_geometryRowHeight - RowHeight) > 0.01
            || Math.Abs(_geometryColumnWidth - ColumnWidth) > 0.01
            || _geometryRowCount != Sheet.RowCount
            || _geometryColumnCount != Sheet.ColumnCount
            || _geometryRowsMetadataCount != Sheet.Rows.Count
            || _geometryColumnsMetadataCount != Sheet.Columns.Count
            || _geometryMergedCount != Sheet.MergedCells.Count
            || _geometryFreezeRowCount != Sheet.FreezeRowCount
            || _geometryFreezeColumnCount != Sheet.FreezeColumnCount;
    }

    private void CaptureStructureSignature()
    {
        _geometrySheet = Sheet;
        _geometryRowHeight = RowHeight;
        _geometryColumnWidth = ColumnWidth;
        _geometryRowCount = Sheet?.RowCount ?? 0;
        _geometryColumnCount = Sheet?.ColumnCount ?? 0;
        _geometryRowsMetadataCount = Sheet?.Rows.Count ?? 0;
        _geometryColumnsMetadataCount = Sheet?.Columns.Count ?? 0;
        _geometryMergedCount = Sheet?.MergedCells.Count ?? 0;
        _geometryFreezeRowCount = Sheet?.FreezeRowCount ?? 0;
        _geometryFreezeColumnCount = Sheet?.FreezeColumnCount ?? 0;
    }

    /// <inheritdoc />
    public async Task FocusAsync()
    {
        try { await _rootElement.FocusAsync(); } catch { }
    }

    /// <inheritdoc />
    public void SelectAllCells()
    {
        if (Sheet is null) return;
        CommitEditIfNeeded();
        _selection.SetActiveCell("A1");
        _selection.SelectionEndRef = SpreadsheetSelectionState.ToCellRef(Sheet.RowCount - 1, Sheet.ColumnCount - 1);
        Sheet.ActiveCellRef = "A1";
        _ = ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
        _needsRender = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSelectedCellRefs() =>
        Sheet is null ? [] : _selection.GetSelectedCellRefs(Sheet);

    /// <inheritdoc />
    public void AppendEditValue(string text)
    {
        _editValue = (_editValue ?? string.Empty) + text;
        _shouldFocusEditorAfterRender = true;
        _needsRender = true;
        StateHasChanged();
    }

    /// <inheritdoc />
    public void InsertCellRefIntoFormula(string cellRef)
    {
        _editValue = FormulaReferenceAdjuster.InsertOrReplaceLastRef(_editValue ?? "=", cellRef);
        _shouldFocusEditorAfterRender = true;
        _needsRender = true;
        StateHasChanged();
    }

    [JSInvokable]
    public Task OnCanvasViewportChanged(double scrollLeft, double scrollTop, double clientWidth, double clientHeight)
    {
        var next = new SpreadsheetViewportState(
            Math.Max(0, scrollLeft),
            Math.Max(0, scrollTop),
            clientWidth > 0 ? clientWidth : SpreadsheetViewportState.Default.Width,
            clientHeight > 0 ? clientHeight : SpreadsheetViewportState.Default.Height);

        if (Math.Abs(_viewport.ScrollLeft - next.ScrollLeft) < 0.5
            && Math.Abs(_viewport.ScrollTop - next.ScrollTop) < 0.5
            && Math.Abs(_viewport.Width - next.Width) < 0.5
            && Math.Abs(_viewport.Height - next.Height) < 0.5)
        {
            return Task.CompletedTask;
        }

        _viewport = next;
        _needsRender = true;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnCanvasPointer(double contentX, double contentY, bool shiftKey, bool ctrlKey)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        if (contentY < 0 && contentX >= 0)
        {
            var col = Math.Clamp(_geometry.FindColumnAtOffset(contentX), 0, Sheet.ColumnCount - 1);
            SelectColumn(col);
            return Task.CompletedTask;
        }

        if (contentX < 0 && contentY >= 0)
        {
            var row = Math.Clamp(_geometry.FindRowAtOffset(contentY), 0, Sheet.RowCount - 1);
            SelectRow(row);
            return Task.CompletedTask;
        }

        var (hitRow, hitCol) = _geometry.HitTest(contentX, contentY);
        if (hitRow < 0 || hitCol < 0)
            return Task.CompletedTask;

        var cellRef = SpreadsheetSelectionState.ToCellRef(hitRow, hitCol);
        if (IsFormatPainterActive)
        {
            SetActiveCell(hitRow, hitCol, extendSelection: false);
            _ = OnFormatPainterApply.InvokeAsync(cellRef);
            return Task.CompletedTask;
        }

        if (IsInFormulaPointMode && !string.Equals(cellRef, Sheet.ActiveCellRef, StringComparison.OrdinalIgnoreCase))
        {
            _ = OnCellReferenceRequested.InvokeAsync(cellRef);
            return Task.CompletedTask;
        }

        if (IsEditing)
            CommitEdit();

        SetActiveCell(hitRow, hitCol, shiftKey && !string.IsNullOrEmpty(_selection.SelectionStartRef));
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnCanvasCellPointer(int row, int col, bool shiftKey, bool ctrlKey)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        row = Math.Clamp(row, 0, Sheet.RowCount - 1);
        col = Math.Clamp(col, 0, Sheet.ColumnCount - 1);
        var cellRef = SpreadsheetSelectionState.ToCellRef(row, col);

        if (IsFormatPainterActive)
        {
            SetActiveCell(row, col, extendSelection: false, render: false);
            _ = OnFormatPainterApply.InvokeAsync(cellRef);
            return Task.CompletedTask;
        }

        if (IsInFormulaPointMode && !string.Equals(cellRef, Sheet.ActiveCellRef, StringComparison.OrdinalIgnoreCase))
        {
            _ = OnCellReferenceRequested.InvokeAsync(cellRef);
            return Task.CompletedTask;
        }

        if (IsEditing)
            CommitEdit();

        SetActiveCell(row, col, shiftKey && !string.IsNullOrEmpty(_selection.SelectionStartRef), render: false);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnCanvasDoubleClick(double contentX, double contentY)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        var (row, col) = _geometry.HitTest(contentX, contentY);
        if (row >= 0 && col >= 0)
        {
            StartEdit(SpreadsheetSelectionState.ToCellRef(row, col));
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnCanvasCellDoubleClick(int row, int col)
    {
        if (Sheet is null)
            return;

        row = Math.Clamp(row, 0, Sheet.RowCount - 1);
        col = Math.Clamp(col, 0, Sheet.ColumnCount - 1);
        var cellRef = SpreadsheetSelectionState.ToCellRef(row, col);
        await InvokeAsync(() =>
        {
            StartEdit(cellRef);
            StateHasChanged();
        });
    }

    [JSInvokable]
    public Task OnCanvasCellEditCommitted(int row, int col, string? value)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        row = Math.Clamp(row, 0, Sheet.RowCount - 1);
        col = Math.Clamp(col, 0, Sheet.ColumnCount - 1);
        var cellRef = SpreadsheetSelectionState.ToCellRef(row, col);
        SetActiveCell(row, col, extendSelection: false, render: false);
        _ = CellValueCommitted.InvokeAsync((cellRef, value));
        _ = OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet, cellRef, false));
        _needsRender = true;
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnCanvasContextMenu(double contentX, double contentY, double clientX, double clientY)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        var (row, col) = _geometry.HitTest(contentX, contentY);
        if (row >= 0 && col >= 0)
            SetActiveCell(row, col, extendSelection: false);

        _contextMenuRowIndex = Math.Max(0, row);
        _contextMenuColIndex = Math.Max(0, col);
        _contextMenuX = clientX;
        _contextMenuY = clientY;
        _contextMenuVisible = true;
        _needsRender = true;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnCanvasColumnResize(int colIndex, double width)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        _geometry.Clear();
        _ = OnColumnResizeRequested.InvokeAsync((Math.Clamp(colIndex, 0, Sheet.ColumnCount - 1), Math.Max(16, width)));
        _needsRender = true;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnCanvasRowResize(int rowIndex, double height)
    {
        if (Sheet is null)
            return Task.CompletedTask;

        _geometry.Clear();
        _ = OnRowResizeRequested.InvokeAsync((Math.Clamp(rowIndex, 0, Sheet.RowCount - 1), Math.Max(8, height)));
        _needsRender = true;
        return InvokeAsync(StateHasChanged);
    }

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (IsEditing || Sheet is null)
            return;

        if (e.CtrlKey)
        {
            switch (e.Key)
            {
                case "c": _ = OnCopyRequested.InvokeAsync(); return;
                case "v": _ = OnPasteRequested.InvokeAsync(); return;
                case "x": _ = OnCutRequested.InvokeAsync(); return;
                case "z": _ = OnUndoRequested.InvokeAsync(); return;
                case "y": _ = OnRedoRequested.InvokeAsync(); return;
                case "b": _ = OnBoldToggleRequested.InvokeAsync(); return;
                case "i": _ = OnItalicToggleRequested.InvokeAsync(); return;
                case "u": _ = OnUnderlineToggleRequested.InvokeAsync(); return;
                case "a": _ = OnSelectAllRequested.InvokeAsync(); return;
                case "1": _ = OnFormatCellsRequested.InvokeAsync(); return;
                case "5": _ = OnStrikeThroughToggleRequested.InvokeAsync(); return;
                case "Home": MoveToCell(0, 0, e.ShiftKey); return;
                case "End": MoveToLastUsedCell(e.ShiftKey); return;
            }
        }

        switch (e.Key)
        {
            case "ArrowUp": MoveActiveCell(-1, 0, e.ShiftKey); break;
            case "ArrowDown": MoveActiveCell(1, 0, e.ShiftKey); break;
            case "ArrowLeft": MoveActiveCell(0, -1, e.ShiftKey); break;
            case "ArrowRight": MoveActiveCell(0, 1, e.ShiftKey); break;
            case "Tab": MoveActiveCell(0, e.ShiftKey ? -1 : 1); break;
            case "Home":
                var (row, _) = SpreadsheetSelectionState.ParseCellRef(Sheet.ActiveCellRef ?? "A1");
                MoveToCell(row, 0, e.ShiftKey);
                break;
            case "End":
                var (activeRow, _) = SpreadsheetSelectionState.ParseCellRef(Sheet.ActiveCellRef ?? "A1");
                MoveToCell(activeRow, Sheet.ColumnCount - 1, e.ShiftKey);
                break;
            case "Enter":
            case "F2":
                StartEdit(Sheet.ActiveCellRef ?? "A1");
                break;
            case "Escape":
                if (IsFormatPainterActive)
                    _ = OnFormatPainterCancel.InvokeAsync();
                else
                    _selection.SelectionEndRef = _selection.SelectionStartRef;
                break;
            case "Delete":
                _ = OnDeleteRequested.InvokeAsync();
                break;
            default:
                if (e.Key.Length == 1 && !e.AltKey && !e.CtrlKey && !e.MetaKey)
                    StartEdit(Sheet.ActiveCellRef ?? "A1", e.Key);
                break;
        }

        _needsRender = true;
    }

    private void HandleEditKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                CommitEdit();
                MoveActiveCell(e.ShiftKey ? -1 : 1, 0);
                break;
            case "Escape":
                CancelEdit();
                break;
            case "Tab":
                CommitEdit();
                MoveActiveCell(0, e.ShiftKey ? -1 : 1);
                break;
            case "F4":
                if (_editValue?.StartsWith("=") == true)
                    _editValue = FormulaReferenceAdjuster.CycleLastAbsoluteRef(_editValue);
                break;
        }
    }

    private void MoveActiveCell(int dRow, int dCol, bool extendSelection = false)
    {
        if (Sheet is null) return;
        var (row, col) = SpreadsheetSelectionState.ParseCellRef(Sheet.ActiveCellRef ?? "A1");
        MoveToCell(Math.Clamp(row + dRow, 0, Sheet.RowCount - 1), Math.Clamp(col + dCol, 0, Sheet.ColumnCount - 1), extendSelection);
    }

    private void MoveToCell(int row, int col, bool extendSelection = false)
    {
        SetActiveCell(row, col, extendSelection);
        _ = EnsureCellVisibleAsync(row, col);
    }

    private void MoveToLastUsedCell(bool extendSelection = false)
    {
        if (Sheet is null) return;
        var lastRow = 0;
        var lastCol = 0;
        foreach (var cellRef in Sheet.Cells.Keys)
        {
            var (row, col) = SpreadsheetSelectionState.ParseCellRef(cellRef);
            if (row > lastRow) lastRow = row;
            if (col > lastCol) lastCol = col;
        }
        MoveToCell(lastRow, lastCol, extendSelection);
    }

    private void SetActiveCell(int row, int col, bool extendSelection)
        => SetActiveCell(row, col, extendSelection, render: true);

    private void SetActiveCell(int row, int col, bool extendSelection, bool render)
    {
        if (Sheet is null) return;
        var cellRef = SpreadsheetSelectionState.ToCellRef(row, col);
        if (extendSelection)
            _selection.ExtendTo(cellRef);
        else
            _selection.SetActiveCell(cellRef);

        Sheet.ActiveCellRef = cellRef;
        _shouldFocusAfterRender = true;
        if (render)
        {
            _ = ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
            _needsRender = true;
            StateHasChanged();
        }
    }

    private void SelectRow(int rowIndex)
    {
        if (Sheet is null) return;
        CommitEditIfNeeded();
        var startRef = SpreadsheetSelectionState.ToCellRef(rowIndex, 0);
        _selection.SetActiveCell(startRef);
        _selection.SelectionEndRef = SpreadsheetSelectionState.ToCellRef(rowIndex, Sheet.ColumnCount - 1);
        Sheet.ActiveCellRef = startRef;
        _ = ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
        _needsRender = true;
        StateHasChanged();
    }

    private void SelectColumn(int colIndex)
    {
        if (Sheet is null) return;
        CommitEditIfNeeded();
        var startRef = SpreadsheetSelectionState.ToCellRef(0, colIndex);
        _selection.SetActiveCell(startRef);
        _selection.SelectionEndRef = SpreadsheetSelectionState.ToCellRef(Sheet.RowCount - 1, colIndex);
        Sheet.ActiveCellRef = startRef;
        _ = ActiveCellChanged.InvokeAsync(Sheet.ActiveCellRef);
        _needsRender = true;
        StateHasChanged();
    }

    private void StartEdit(string cellRef, string? initialValue = null)
    {
        if (Sheet is null)
            return;

        if (!string.Equals(Sheet.ActiveCellRef, cellRef, StringComparison.OrdinalIgnoreCase))
        {
            var (row, col) = SpreadsheetSelectionState.ParseCellRef(cellRef);
            SetActiveCell(row, col, extendSelection: false, render: false);
        }

        IsEditing = true;
        _editValue = initialValue;
        if (_editValue is null && Sheet.Cells.TryGetValue(cellRef, out var cell))
            _editValue = cell.Formula ?? cell.Value?.ToString() ?? string.Empty;

        _shouldFocusEditorAfterRender = true;
        _needsRender = true;
        _ = OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet, cellRef, true));
        StateHasChanged();
    }

    private void OnEditInput(ChangeEventArgs e)
    {
        _editValue = e.Value?.ToString();
        _needsRender = true;
    }

    private void OnEditBlur(FocusEventArgs e)
    {
        if (!IsInFormulaPointMode)
            CommitEdit();
    }

    private void CommitEditIfNeeded()
    {
        if (IsEditing)
            CommitEdit();
    }

    private void CommitEdit()
    {
        if (!IsEditing)
            return;

        IsEditing = false;
        var cellRef = Sheet?.ActiveCellRef;
        if (cellRef is not null && _editValue is not null)
            _ = CellValueCommitted.InvokeAsync((cellRef, _editValue));

        _ = OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet!, cellRef ?? "A1", false));
        _editValue = null;
        _shouldFocusAfterRender = true;
        _needsRender = true;
        StateHasChanged();
    }

    private void CancelEdit()
    {
        if (!IsEditing)
            return;

        IsEditing = false;
        _ = OnCellEdit.InvokeAsync(new SpreadsheetCellEditEventArgs(Sheet!, Sheet?.ActiveCellRef ?? "A1", false));
        _editValue = null;
        _shouldFocusAfterRender = true;
        _needsRender = true;
        StateHasChanged();
    }

    private async Task EnsureCellVisibleAsync(int row, int col)
    {
        var rect = _geometry.GetCellRect(row, col);
        var request = new
        {
            Left = SpreadsheetGridConstants.RowHeaderWidth + rect.Left,
            Top = SpreadsheetGridConstants.ColumnHeaderHeight + rect.Top,
            Right = SpreadsheetGridConstants.RowHeaderWidth + rect.Left + rect.Width,
            Bottom = SpreadsheetGridConstants.ColumnHeaderHeight + rect.Top + rect.Height,
            FrozenRow = IsFrozenRow(row),
            FrozenColumn = IsFrozenCol(col)
        };

        try
        {
            await JS.InvokeVoidAsync("tmSpreadsheetCanvas.ensureCellVisible", _rootElement, request, new
            {
                SpreadsheetGridConstants.RowHeaderWidth,
                SpreadsheetGridConstants.ColumnHeaderHeight
            });
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    private string GetEditorStyle()
    {
        if (Sheet?.ActiveCellRef is null)
            return string.Empty;

        var (row, col) = SpreadsheetSelectionState.ParseCellRef(Sheet.ActiveCellRef);
        var rect = _geometry.GetCellRect(row, col);
        return string.Join(' ',
            $"left:{SpreadsheetGridConstants.RowHeaderWidth + rect.Left}px;",
            $"top:{SpreadsheetGridConstants.ColumnHeaderHeight + rect.Top}px;",
            $"width:{Math.Max(20, rect.Width)}px;",
            $"height:{Math.Max(16, rect.Height)}px;");
    }

    private string GetActiveCellDisplayValue()
    {
        if (Sheet?.ActiveCellRef is null)
            return string.Empty;

        var cell = Sheet.Cells.GetValueOrDefault(Sheet.ActiveCellRef);
        return GetCellDisplayValue(Sheet.ActiveCellRef, cell);
    }

    private string GetCellDisplayValue(string cellRef, SpreadsheetCell? cell)
    {
        if (cell is null)
            return string.Empty;

        if (!string.IsNullOrEmpty(cell.Formula) && (cell.DisplayValue is null || cell.Value is null))
            Sheet?.EvaluateFormula(cellRef);

        return !string.IsNullOrEmpty(cell.DisplayValue)
            ? cell.DisplayValue
            : SpreadsheetNumberFormatter.Format(cell.Value, cell.Style.NumberFormat) ?? string.Empty;
    }

    private object BuildCanvasFrame()
    {
        var sheet = Sheet!;
        var rows = GetRenderRows(sheet).ToArray();
        var columns = GetRenderColumns(sheet).ToArray();
        var cells = BuildRenderCells(sheet, rows, columns).ToArray();
        var selection = _selection.GetBounds();

        return new
        {
            RowCount = sheet.RowCount,
            ColumnCount = sheet.ColumnCount,
            RowHeaderWidth = SpreadsheetGridConstants.RowHeaderWidth,
            ColumnHeaderHeight = SpreadsheetGridConstants.ColumnHeaderHeight,
            ScrollLeft = _viewport.ScrollLeft,
            ScrollTop = _viewport.ScrollTop,
            ViewportWidth = _viewport.Width,
            ViewportHeight = _viewport.Height,
            TotalWidth = _geometry.ContentWidth + SpreadsheetGridConstants.RowHeaderWidth,
            TotalHeight = _geometry.ContentHeight + SpreadsheetGridConstants.ColumnHeaderHeight,
            ShowGridLines = sheet.ShowGridLines,
            FreezeRowCount = Math.Clamp(sheet.FreezeRowCount, 0, sheet.RowCount),
            FreezeColumnCount = Math.Clamp(sheet.FreezeColumnCount, 0, sheet.ColumnCount),
            ActiveCellRef = sheet.ActiveCellRef,
            IsFormulaPointMode = IsInFormulaPointMode,
            IsFormatPainterActive,
            Selection = new { selection.StartRow, selection.StartCol, selection.EndRow, selection.EndCol },
            Rows = rows,
            Columns = columns,
            Cells = cells
        };
    }

    private IEnumerable<CanvasRowFrame> GetRenderRows(SpreadsheetSheet sheet)
    {
        var seen = new HashSet<int>();
        var frozenCount = Math.Clamp(sheet.FreezeRowCount, 0, sheet.RowCount);
        for (var row = 0; row < frozenCount; row++)
        {
            if (seen.Add(row))
                yield return BuildRow(row, frozen: true);
        }

        var (start, end) = _geometry.GetVisibleRows(sheet, _viewport, RowOverscanCount);
        for (var row = start; row <= end; row++)
        {
            if (seen.Add(row))
                yield return BuildRow(row, frozen: false);
        }
    }

    private CanvasRowFrame BuildRow(int row, bool frozen) => new(
        row,
        _geometry.GetCumulativeRowHeight(row),
        _geometry.GetRowHeight(row),
        frozen);

    private IEnumerable<CanvasColumnFrame> GetRenderColumns(SpreadsheetSheet sheet)
    {
        var seen = new HashSet<int>();
        var frozenCount = Math.Clamp(sheet.FreezeColumnCount, 0, sheet.ColumnCount);
        for (var col = 0; col < frozenCount; col++)
        {
            if (seen.Add(col))
                yield return BuildColumn(col, frozen: true);
        }

        var viewport = _viewport with { Width = Math.Max(0, _viewport.Width - SpreadsheetGridConstants.RowHeaderWidth) };
        var (start, end) = _geometry.GetVisibleColumns(sheet, viewport, ColumnOverscanCount);
        for (var col = start; col <= end; col++)
        {
            if (seen.Add(col))
                yield return BuildColumn(col, frozen: false);
        }
    }

    private CanvasColumnFrame BuildColumn(int col, bool frozen) => new(
        col,
        _geometry.GetCumulativeColumnWidth(col),
        _geometry.GetColumnWidth(col),
        SpreadsheetRange.ColumnIndexToLetters(col),
        frozen);

    private IEnumerable<object> BuildRenderCells(SpreadsheetSheet sheet, IReadOnlyList<CanvasRowFrame> rows, IReadOnlyList<CanvasColumnFrame> columns)
    {
        foreach (var row in rows)
        {
            var rowIndex = row.Index;
            if (_geometry.GetRowHeight(rowIndex) <= 0)
                continue;

            foreach (var column in columns)
            {
                var colIndex = column.Index;
                if (_geometry.GetColumnWidth(colIndex) <= 0 || _mergedHiddenLookup.Contains((rowIndex, colIndex)))
                    continue;

                var cellRef = SpreadsheetSelectionState.ToCellRef(rowIndex, colIndex);
                sheet.Cells.TryGetValue(cellRef, out var cell);
                var merged = _mergedStartLookup.GetValueOrDefault((rowIndex, colIndex));
                var rect = GetMergedAwareRect(rowIndex, colIndex, merged);
                var selected = IsSelected(rowIndex, colIndex, cellRef);
                yield return new
                {
                    Row = rowIndex,
                    Col = colIndex,
                    Ref = cellRef,
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height,
                    Value = GetCellDisplayValue(cellRef, cell),
                    Active = string.Equals(sheet.ActiveCellRef, cellRef, StringComparison.OrdinalIgnoreCase),
                    Selected = selected,
                    SelectionEnd = IsSelectionEndCell(rowIndex, colIndex),
                    ImageUrl = cell?.ImageUrl,
                    Hyperlink = cell?.Hyperlink,
                    Style = BuildCanvasStyle(cell?.Style, cell)
                };
            }
        }
    }

    private (double Left, double Top, double Width, double Height) GetMergedAwareRect(int row, int col, SpreadsheetRange? merged)
    {
        var rect = _geometry.GetCellRect(row, col);
        if (merged is null)
            return rect;

        var width = rect.Width;
        var height = rect.Height;
        for (var c = merged.StartCol + 1; c <= merged.EndCol; c++)
            width += _geometry.GetColumnWidth(c);
        for (var r = merged.StartRow + 1; r <= merged.EndRow; r++)
            height += _geometry.GetRowHeight(r);
        return (rect.Left, rect.Top, width, height);
    }

    private static object? BuildCanvasStyle(SpreadsheetCellStyle? style, SpreadsheetCell? cell)
    {
        if (style is null || !HasCanvasStylePayload(style, cell))
            return null;

        return new
        {
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline || style.DoubleUnderline,
            DoubleUnderline = style.DoubleUnderline,
            StrikeThrough = style.StrikeThrough,
            ForeColor = IsDefaultForeColor(style.ForeColor) ? null : style.ForeColor,
            BackgroundColor = string.IsNullOrWhiteSpace(style.BackgroundColor) || style.BackgroundColor == "transparent" ? null : style.BackgroundColor,
            HorizontalAlign = GetEffectiveHAlign(style, cell).ToString().ToLowerInvariant(),
            VerticalAlign = style.VerticalAlign.ToString().ToLowerInvariant(),
            TextWrap = style.TextWrap,
            BorderTop = BuildCanvasBorder(style.BorderTop),
            BorderRight = BuildCanvasBorder(style.BorderRight),
            BorderBottom = BuildCanvasBorder(style.BorderBottom),
            BorderLeft = BuildCanvasBorder(style.BorderLeft)
        };
    }

    private static bool HasCanvasStylePayload(SpreadsheetCellStyle style, SpreadsheetCell? cell) =>
        cell?.Value is not null
        || !string.IsNullOrEmpty(cell?.DisplayValue)
        || !string.IsNullOrEmpty(cell?.Formula)
        || !string.IsNullOrEmpty(cell?.Hyperlink)
        || !string.IsNullOrEmpty(cell?.ImageUrl)
        || style.Bold
        || style.Italic
        || style.Underline
        || style.DoubleUnderline
        || style.StrikeThrough
        || style.TextWrap
        || style.HorizontalAlign != SpreadsheetHorizontalAlign.General
        || style.VerticalAlign != SpreadsheetVerticalAlign.Bottom
        || !string.Equals(style.FontFamily, "Calibri", StringComparison.OrdinalIgnoreCase)
        || Math.Abs(style.FontSize - 11) > 0.01
        || !IsDefaultForeColor(style.ForeColor)
        || (!string.IsNullOrWhiteSpace(style.BackgroundColor) && style.BackgroundColor != "transparent")
        || style.BorderTop.Style != SpreadsheetBorderStyle.None
        || style.BorderRight.Style != SpreadsheetBorderStyle.None
        || style.BorderBottom.Style != SpreadsheetBorderStyle.None
        || style.BorderLeft.Style != SpreadsheetBorderStyle.None;

    private static bool IsDefaultForeColor(string? color) =>
        string.IsNullOrWhiteSpace(color)
        || string.Equals(color, "#000000", StringComparison.OrdinalIgnoreCase)
        || string.Equals(color, "black", StringComparison.OrdinalIgnoreCase);

    private static object BuildCanvasBorder(SpreadsheetBorder border) => new
    {
        Style = border.Style.ToString().ToLowerInvariant(),
        Color = border.Color
    };

    private static SpreadsheetHorizontalAlign GetEffectiveHAlign(SpreadsheetCellStyle style, SpreadsheetCell? cell)
    {
        if (style.HorizontalAlign != SpreadsheetHorizontalAlign.General)
            return style.HorizontalAlign;
        return cell?.Value switch
        {
            double or decimal or float or int or long or short or byte => SpreadsheetHorizontalAlign.Right,
            bool => SpreadsheetHorizontalAlign.Center,
            _ => SpreadsheetHorizontalAlign.Left
        };
    }

    private bool IsSelected(int row, int col, string cellRef)
    {
        if (string.Equals(Sheet?.ActiveCellRef, cellRef, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!_selection.HasRangeSelection)
            return false;
        var bounds = _selection.GetBounds();
        return row >= bounds.StartRow && row <= bounds.EndRow && col >= bounds.StartCol && col <= bounds.EndCol;
    }

    private bool IsSelectionEndCell(int row, int col)
    {
        if (!_selection.HasRangeSelection)
            return string.Equals(Sheet?.ActiveCellRef, SpreadsheetSelectionState.ToCellRef(row, col), StringComparison.OrdinalIgnoreCase);
        var bounds = _selection.GetBounds();
        return row == bounds.EndRow && col == bounds.EndCol;
    }

    private bool IsFrozenRow(int row) => Sheet?.FreezeRowCount > 0 && row < Sheet.FreezeRowCount;
    private bool IsFrozenCol(int col) => Sheet?.FreezeColumnCount > 0 && col < Sheet.FreezeColumnCount;

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
                    if (row != range.StartRow || col != range.StartCol)
                        _mergedHiddenLookup.Add((row, col));
                }
            }
        }
    }

    private void ContextMenuFormatCells() { CloseContextMenu(); _ = OnFormatCellsRequested.InvokeAsync(); }
    private void ContextMenuCopy() { CloseContextMenu(); _ = OnCopyRequested.InvokeAsync(); }
    private void ContextMenuCut() { CloseContextMenu(); _ = OnCutRequested.InvokeAsync(); }
    private void ContextMenuPaste() { CloseContextMenu(); _ = OnPasteRequested.InvokeAsync(); }
    private void ContextMenuInsertRow() { CloseContextMenu(); _ = OnInsertRowRequested.InvokeAsync(); }
    private void ContextMenuDeleteRow() { CloseContextMenu(); _ = OnDeleteRowRequested.InvokeAsync(); }
    private void ContextMenuInsertColumn() { CloseContextMenu(); _ = OnInsertColumnRequested.InvokeAsync(); }
    private void ContextMenuDeleteColumn() { CloseContextMenu(); _ = OnDeleteColumnRequested.InvokeAsync(); }
    private void ContextMenuClearFormatting() { CloseContextMenu(); _ = OnClearFormattingRequested.InvokeAsync(); }
    private void ContextMenuClearContent() { CloseContextMenu(); _ = OnClearContentRequested.InvokeAsync(); }
    private void ContextMenuClearAll() { CloseContextMenu(); _ = OnClearAllRequested.InvokeAsync(); }

    private void ContextMenuSetColumnWidth()
    {
        CloseContextMenu();
        _colWidthInputValue = ((int)Math.Round(_geometry.GetColumnWidth(_contextMenuColIndex))).ToString();
        _showColWidthDialog = true;
    }

    private void ContextMenuSetRowHeight()
    {
        CloseContextMenu();
        _rowHeightInputValue = ((int)Math.Round(_geometry.GetRowHeight(_contextMenuRowIndex))).ToString();
        _showRowHeightDialog = true;
    }

    private void CloseContextMenu() => _contextMenuVisible = false;

    private void ApplyColWidth()
    {
        if (double.TryParse(_colWidthInputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var width))
        {
            _ = OnColumnResizeRequested.InvokeAsync((_contextMenuColIndex, Math.Max(16, width)));
            _geometry.Clear();
            _needsRender = true;
        }
        _showColWidthDialog = false;
    }

    private void ApplyRowHeight()
    {
        if (double.TryParse(_rowHeightInputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var height))
        {
            _ = OnRowResizeRequested.InvokeAsync((_contextMenuRowIndex, Math.Max(8, height)));
            _geometry.Clear();
            _needsRender = true;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await RegisterCanvasAsync();

        if (_needsRender && Sheet is not null)
        {
            _needsRender = false;
            try { await JS.InvokeVoidAsync("tmSpreadsheetCanvas.render", _rootElement, _canvasElement, BuildCanvasFrame()); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        if (_shouldFocusEditorAfterRender)
        {
            _shouldFocusEditorAfterRender = false;
            try { await _editInput.FocusAsync(); } catch { }
        }
        else if (_shouldFocusAfterRender)
        {
            _shouldFocusAfterRender = false;
            try { await _rootElement.FocusAsync(); } catch { }
        }
    }

    private async Task RegisterCanvasAsync()
    {
        if (_registered)
            return;

        _dotNetRef ??= DotNetObjectReference.Create(this);
        try
        {
            await JS.InvokeVoidAsync("tmSpreadsheetCanvas.register", _rootElement, _canvasElement, _dotNetRef);
            _registered = true;
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_registered)
        {
            try { await JS.InvokeVoidAsync("tmSpreadsheetCanvas.dispose", _rootElement); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }

        _dotNetRef?.Dispose();
    }
}
