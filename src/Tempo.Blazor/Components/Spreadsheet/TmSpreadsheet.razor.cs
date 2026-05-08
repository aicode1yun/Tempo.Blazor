using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Rendering;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// An Excel-like spreadsheet component for viewing and editing tabular data.
/// Supports cell editing, formulas, styling, multi-sheet workbooks, and XLSX import/export.
/// </summary>
public partial class TmSpreadsheet
{
    private ISpreadsheetGridController? _grid;
    private TmSpreadsheetFormulaBar? _formulaBar;
    private SpreadsheetWorkbook _workbook = new();
    private SpreadsheetCommandManager? _commandManager;
    private bool _isFormulaBarEditing;
    private string? _formulaBarEditValue;
    private bool _showInsertLinkDialog;
    private bool _showInsertImageDialog;
    private bool _showFormatCellsDialog;
    private SpreadsheetCellStyle _formatCellsStyle = new();
    private SpreadsheetCellStyle? _formatPainterStyle;
    private bool _formatPainterActive;
    private bool _formatPainterSticky;
    private string? _insertLinkUrl;
    private string? _insertLinkText;
    private string? _insertImageUrl;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    private System.Threading.CancellationTokenSource? _onChangeDebounceCts;
    private int _formulaOriginSheetIndex = -1;
    private string? _formulaOriginCellRef;
    private bool IsFormulaBarSessionActive => _isFormulaBarEditing || _formulaBarEditValue is not null;
    private bool HostFormulaPointMode => (_formulaBar?.CurrentEditValue ?? _formulaBarEditValue)?.StartsWith("=", StringComparison.Ordinal) == true;
    private string FormulaCultureName => CultureInfo.CurrentCulture.Name;
    private string FormulaDecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? "," : ".";
    private string FormulaArgumentSeparator => FormulaDecimalSeparator == "," ? ";" : ",";

    /// <summary>XLSX file data to load into the spreadsheet.</summary>
    [Parameter] public byte[]? Data { get; set; }

    /// <summary>Called when a file is opened/imported.</summary>
    [Parameter] public EventCallback<SpreadsheetOpenEventArgs> OnOpen { get; set; }

    /// <summary>Called when the workbook is downloaded/exported.</summary>
    [Parameter] public EventCallback<SpreadsheetDownloadEventArgs> OnDownload { get; set; }

    /// <summary>Called when any cell value or formula changes.</summary>
    [Parameter] public EventCallback<SpreadsheetChangeEventArgs> OnChange { get; set; }

    /// <summary>Called when the active cell or selection changes.</summary>
    [Parameter] public EventCallback<SpreadsheetSelectEventArgs> OnSelect { get; set; }

    /// <summary>Called when a cell enters or exits edit mode.</summary>
    [Parameter] public EventCallback<SpreadsheetCellEditEventArgs> OnCellEdit { get; set; }

    /// <summary>The CSS height of the spreadsheet container. Defaults to 600px.</summary>
    [Parameter] public string? Height { get; set; } = "600px";

    /// <summary>The CSS width of the spreadsheet container. Defaults to 100%.</summary>
    [Parameter] public string? Width { get; set; } = "100%";

    /// <summary>The initial number of rows to render. Defaults to 200.</summary>
    [Parameter] public int RowsCount { get; set; } = 200;

    /// <summary>The initial number of columns to render. Defaults to 50.</summary>
    [Parameter] public int ColumnsCount { get; set; } = 50;

    /// <summary>The default row height in pixels. Defaults to 20.</summary>
    [Parameter] public double RowHeight { get; set; } = 20;

    /// <summary>The default column width in pixels. Defaults to 64.</summary>
    [Parameter] public double ColumnWidth { get; set; } = 64;

    /// <summary>Renderer used for the spreadsheet grid surface. Defaults to DOM for full compatibility.</summary>
    [Parameter] public SpreadsheetRenderMode RenderMode { get; set; } = SpreadsheetRenderMode.Dom;

    /// <summary>Additional CSS classes to apply to the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes to apply to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Gets the underlying workbook for programmatic access.</summary>
    public SpreadsheetWorkbook Workbook => _workbook;

    private bool UseCanvasJsEngine => RenderMode == SpreadsheetRenderMode.CanvasJsEngine;
    private TmSpreadsheetCanvasGrid? CanvasJsEngineGrid => UseCanvasJsEngine ? _grid as TmSpreadsheetCanvasGrid : null;

    // ── Toolbar state ──
    private bool CanUndo => _commandManager?.CanUndo ?? false;
    private bool CanRedo => _commandManager?.CanRedo ?? false;

    private string? ToolbarFontFamily => GetActiveCellStyle()?.FontFamily;
    private string? ToolbarFontSize => GetActiveCellStyle()?.FontSize.ToString();
    private bool ToolbarIsBold => GetActiveCellStyle()?.Bold ?? false;
    private bool ToolbarIsItalic => GetActiveCellStyle()?.Italic ?? false;
    private bool ToolbarIsUnderline => GetActiveCellStyle()?.Underline ?? false;
    private bool ToolbarIsStrikeThrough => GetActiveCellStyle()?.StrikeThrough ?? false;
    private bool ToolbarIsFormatPainterActive => _formatPainterActive;
    private string? ToolbarTextColor => GetActiveCellStyle()?.ForeColor;
    private string? ToolbarBackgroundColor => GetActiveCellStyle()?.BackgroundColor;
    private string? ToolbarAlign
    {
        get
        {
            var style = GetActiveCellStyle();
            if (style is null) return null;
            if (style.HorizontalAlign != SpreadsheetHorizontalAlign.General)
                return style.HorizontalAlign.ToString().ToLowerInvariant();
            var cellRef = _workbook.ActiveSheet?.ActiveCellRef;
            var cell = cellRef is null ? null : _workbook.ActiveSheet?.Cells.GetValueOrDefault(cellRef);
            return cell?.Value switch
            {
                double => "right",
                bool => "center",
                _ => "left"
            };
        }
    }
    private string? ToolbarNumberFormat => GetActiveCellStyle()?.NumberFormat ?? "General";
    private bool ToolbarIsPercentageFormat => (GetActiveCellStyle()?.NumberFormat ?? "General").Contains('%');
    private bool ToolbarIsThousandsFormat => (GetActiveCellStyle()?.NumberFormat ?? "General").Contains("#,#");
    private bool IsMergeCellsActive
    {
        get
        {
            if (_workbook.ActiveSheet?.MergedCells is null || _grid is null) return false;
            var bounds = GetSelectionBounds();
            return _workbook.ActiveSheet.MergedCells.Any(r =>
                r.StartRow == bounds.StartRow && r.StartCol == bounds.StartCol
                && r.EndRow == bounds.EndRow && r.EndCol == bounds.EndCol);
        }
    }

    private (int StartRow, int StartCol, int EndRow, int EndCol) GetSelectionBounds()
    {
        if (_workbook.ActiveSheet is null || _grid is null) return (0, 0, 0, 0);
        var start = ParseCellRef(_grid.SelectionStartRef ?? _workbook.ActiveSheet.ActiveCellRef ?? "A1");
        var end = ParseCellRef(_grid.SelectionEndRef ?? _grid.SelectionStartRef ?? _workbook.ActiveSheet.ActiveCellRef ?? "A1");
        return (
            Math.Min(start.Row, end.Row),
            Math.Min(start.Col, end.Col),
            Math.Max(start.Row, end.Row),
            Math.Max(start.Col, end.Col)
        );
    }

    private void InvalidateRenderedCells(IEnumerable<string> cellRefs)
    {
        _grid?.InvalidateRenderedCells(ExpandActiveSheetAffectedCellRefs(cellRefs));
    }

    private void InvalidateRenderedRows(IEnumerable<int> rowIndices)
    {
        _grid?.InvalidateRenderedRows(rowIndices);
    }

    private void InvalidateRenderedColumns(IEnumerable<int> columnIndices)
    {
        _grid?.InvalidateRenderedColumns(columnIndices);
    }

    private void ClearRenderedCache()
    {
        _grid?.ClearRenderedCache();
    }

    private void RequestCanvasJsEngineFullRender()
    {
        CanvasJsEngineGrid?.RequestFullRender();
    }

    private Task SyncCanvasJsEngineCellsAsync(IEnumerable<string> cellRefs)
    {
        var grid = CanvasJsEngineGrid;
        return grid is null
            ? Task.CompletedTask
            : grid.ApplyEngineCellPatchesAsync(ExpandActiveSheetAffectedCellRefs(cellRefs));
    }

    private IReadOnlyList<string> ExpandActiveSheetAffectedCellRefs(IEnumerable<string> cellRefs)
    {
        var refs = cellRefs
            .Where(static cellRef => !string.IsNullOrWhiteSpace(cellRef))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (refs.Length == 0)
            return refs;

        return _workbook.ActiveSheet?.GetCellAndDependentRefs(refs) ?? refs;
    }

    private Task PreviewCanvasJsEngineStyleAsync(IEnumerable<string> cellRefs, Action<SpreadsheetCellStyle> mutate)
    {
        var grid = CanvasJsEngineGrid;
        return grid is null
            ? Task.CompletedTask
            : grid.PreviewEngineStylePatchesAsync(cellRefs, mutate);
    }

    private bool ShowGridLines => _workbook.ActiveSheet?.ShowGridLines ?? true;

    private SpreadsheetCellStyle? GetActiveCellStyle()
    {
        var cellRef = _workbook.ActiveSheet?.ActiveCellRef;
        if (cellRef is null) return null;
        return _workbook.ActiveSheet?.Cells.GetValueOrDefault(cellRef)?.Style;
    }

    private string? GetActiveCellEditValue()
    {
        var cellRef = _workbook.ActiveSheet?.ActiveCellRef;
        if (cellRef is null) return null;
        var cell = _workbook.ActiveSheet?.Cells.GetValueOrDefault(cellRef);
        if (cell?.Formula is not null) return cell.Formula;
        return cell?.Value?.ToString();
    }

    protected override void OnParametersSet()
    {
        if (_workbook.ActiveSheet is not null)
        {
            _workbook.ActiveSheet.RowCount = RowsCount;
            _workbook.ActiveSheet.ColumnCount = ColumnsCount;
            _workbook.ActiveSheet.DefaultRowHeight = RowHeight;
            _workbook.ActiveSheet.DefaultColumnWidth = ColumnWidth;
            _commandManager ??= new SpreadsheetCommandManager(_workbook.ActiveSheet);
        }
    }

    // ── Grid events ──
    private void OnGridActiveCellChanged(string? cellRef)
    {
        if (IsFormulaBarSessionActive)
        {
            StateHasChanged();
            return;
        }
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        if (_workbook.ActiveSheet is not null && cellRef is not null)
        {
            _ = OnSelect.InvokeAsync(new SpreadsheetSelectEventArgs(
                _workbook.ActiveSheet,
                cellRef,
                _grid?.SelectionStartRef,
                _grid?.SelectionEndRef));
        }
        StateHasChanged();
    }

    private void OnGridCellEdit(SpreadsheetCellEditEventArgs args)
    {
        if (!args.IsEditing && _formulaOriginSheetIndex >= 0)
        {
            var originIdx = _formulaOriginSheetIndex;
            _formulaOriginSheetIndex = -1;
            _formulaOriginCellRef = null;
            if (_workbook.ActiveSheetIndex != originIdx)
            {
                _workbook.ActiveSheetIndex = originIdx;
                _commandManager = _workbook.ActiveSheet is not null
                    ? new SpreadsheetCommandManager(_workbook.ActiveSheet)
                    : null;
            }
        }
        _ = OnCellEdit.InvokeAsync(args);
    }

    private async Task OnColumnResizeRequested((int ColIndex, double Width) args)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        _commandManager.Execute(new ResizeColumnCommand(_workbook.ActiveSheet, args.ColIndex, args.Width));
        InvalidateRenderedColumns(new[] { args.ColIndex });
        if (CanvasJsEngineGrid is not null)
            await CanvasJsEngineGrid.ApplyEngineLayoutPatchesAsync(columnIndices: new[] { args.ColIndex });
        StateHasChanged();
    }

    private async Task OnRowResizeRequested((int RowIndex, double Height) args)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        _commandManager.Execute(new ResizeRowCommand(_workbook.ActiveSheet, args.RowIndex, args.Height));
        InvalidateRenderedRows(new[] { args.RowIndex });
        if (CanvasJsEngineGrid is not null)
            await CanvasJsEngineGrid.ApplyEngineLayoutPatchesAsync(rowIndices: new[] { args.RowIndex });
        StateHasChanged();
    }

    private async Task OnGridCellReferenceRequested(string cellRef)
    {
        var fullRef = cellRef;
        if (_formulaOriginSheetIndex >= 0 && _workbook.ActiveSheetIndex != _formulaOriginSheetIndex)
        {
            var sheetName = _workbook.Sheets[_workbook.ActiveSheetIndex].Name;
            var quotedName = sheetName.Contains(' ') ? $"'{sheetName}'" : sheetName;
            fullRef = $"{quotedName}!{cellRef}";
        }

        if (IsFormulaBarSessionActive && _formulaBar is not null)
        {
            await InsertFormulaBarReferenceAsync(fullRef);
            return;
        }

        _grid?.InsertCellRefIntoFormula(fullRef);
        _formulaBarEditValue = _grid?.CurrentEditValue;
        StateHasChanged();
    }

    private async Task InsertFormulaBarReferenceAsync(string fullRef)
    {
        if (_formulaBar is null)
            return;

        await _formulaBar.ReplaceReferenceAsync(fullRef);
        _formulaBarEditValue = _formulaBar.CurrentEditValue;
        StateHasChanged();
    }

    private void OnGridSheetSwitchForFormula(int index)
    {
        if (index < 0 || index >= _workbook.Sheets.Count) return;
        if (_formulaOriginSheetIndex < 0)
        {
            _formulaOriginSheetIndex = _workbook.ActiveSheetIndex;
            _formulaOriginCellRef = _workbook.ActiveSheet?.ActiveCellRef;
        }
        _workbook.ActiveSheetIndex = index;
        StateHasChanged();
    }

    private async Task OnGridCellValueCommitted((string CellRef, string? Value) args)
    {
        if (_commandManager is null || args.Value is null) return;

        // In cross-sheet formula-point mode commit to origin sheet/cell
        var targetSheet = (_formulaOriginSheetIndex >= 0 && _formulaOriginCellRef is not null
            && _formulaOriginSheetIndex < _workbook.Sheets.Count)
            ? _workbook.Sheets[_formulaOriginSheetIndex]
            : _workbook.ActiveSheet;
        var targetCellRef = (_formulaOriginSheetIndex >= 0 && _formulaOriginCellRef is not null)
            ? _formulaOriginCellRef
            : args.CellRef;

        if (targetSheet is null) return;
        var previous = targetSheet.Cells.GetValueOrDefault(targetCellRef);
        var cmd = new SetCellValueCommand(
            targetSheet,
            targetCellRef,
            args.Value.StartsWith('=') ? null : args.Value,
            args.Value.StartsWith('=') ? args.Value : null);
        _commandManager.Execute(cmd);
        _ = OnChange.InvokeAsync(new SpreadsheetChangeEventArgs(
            targetSheet,
            targetCellRef,
            previous?.Value,
            args.Value.StartsWith('=') ? null : args.Value,
            previous?.Formula,
            args.Value.StartsWith('=') ? args.Value : null));
        InvalidateRenderedCells(new[] { targetCellRef });
        if (ReferenceEquals(targetSheet, _workbook.ActiveSheet))
            await SyncCanvasJsEngineCellsAsync(new[] { targetCellRef });
        if (!_isFormulaBarEditing)
        {
            _formulaBarEditValue = null;
        }
        StateHasChanged();
    }

    private async Task OnGridCellValuesCommittedBatch(IReadOnlyList<TmSpreadsheetCanvasGrid.CanvasCellEditCommit> commits)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null || commits.Count == 0)
            return;

        var targetSheet = _workbook.ActiveSheet;
        var refs = new List<string>(commits.Count);
        var previousByRef = new Dictionary<string, SpreadsheetCell?>(StringComparer.OrdinalIgnoreCase);
        var batch = new BatchCommand();

        foreach (var commit in commits)
        {
            var row = Math.Clamp(commit.Row, 0, targetSheet.RowCount - 1);
            var col = Math.Clamp(commit.Col, 0, targetSheet.ColumnCount - 1);
            var cellRef = SpreadsheetSelectionState.ToCellRef(row, col);
            refs.Add(cellRef);
            if (!previousByRef.ContainsKey(cellRef))
                previousByRef[cellRef] = targetSheet.Cells.GetValueOrDefault(cellRef)?.Clone();

            var value = commit.Value ?? string.Empty;
            batch.Add(new SetCellValueCommand(
                targetSheet,
                cellRef,
                value.StartsWith('=') ? null : value,
                value.StartsWith('=') ? value : null));
        }

        _commandManager.Execute(batch);
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);

        foreach (var cellRef in refs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            previousByRef.TryGetValue(cellRef, out var previous);
            var current = targetSheet.Cells.GetValueOrDefault(cellRef);
            _ = OnChange.InvokeAsync(new SpreadsheetChangeEventArgs(
                targetSheet,
                cellRef,
                previous?.Value,
                current?.Value,
                previous?.Formula,
                current?.Formula));
        }

        if (!_isFormulaBarEditing)
        {
            _formulaBarEditValue = null;
        }
        StateHasChanged();
    }

    // ── Formula bar events ──
    private void OnFormulaBarEditStarted()
    {
        _isFormulaBarEditing = true;
        _formulaBarEditValue = GetActiveCellEditValue();
        _formulaOriginSheetIndex = -1;
        _formulaOriginCellRef = null;
        // Also start editing in the grid so the cell shows input
        if (_workbook.ActiveSheet?.ActiveCellRef is { } cellRef)
        {
            // We can't directly call StartEdit because it's private,
            // but we can simulate the effect by setting the cell value
            // when committed. For visual sync the formula bar handles its own input.
        }
        StateHasChanged();
    }

    private async Task OnFormulaBarCommitted(string? value)
    {
        _isFormulaBarEditing = false;
        await ApplyValueToActiveCellAsync(value);
        var navigation = _formulaBar?.ConsumePendingCommitNavigation();
        if (navigation is { } move && _grid is not null)
            await _grid.MoveActiveCellByAsync(move.RowDelta, move.ColDelta);
        _formulaBarEditValue = null;
        StateHasChanged();
    }

    private void OnFormulaBarCancelled()
    {
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        _formulaOriginSheetIndex = -1;
        _formulaOriginCellRef = null;
        StateHasChanged();
    }

    private void OnFormulaBarValueChanged(string? value)
    {
        _formulaBarEditValue = value;
        StateHasChanged();
    }

    private async Task OnFormulaBarTabPressed()
    {
        if (_grid is not null)
            await _grid.FocusAsync();
    }

    private async Task OnFormulaBarTransferToInlineEditorRequested()
    {
        if (_grid is null)
            return;

        await _grid.BeginInlineEditAsync();
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        StateHasChanged();
    }

    private async Task ApplyValueToActiveCellAsync(string? value)
    {
        if (value is null || _commandManager is null) return;

        var targetSheet = (_formulaOriginSheetIndex >= 0 && _formulaOriginCellRef is not null
            && _formulaOriginSheetIndex < _workbook.Sheets.Count)
            ? _workbook.Sheets[_formulaOriginSheetIndex]
            : _workbook.ActiveSheet;
        var cellRef = (_formulaOriginSheetIndex >= 0 && _formulaOriginCellRef is not null)
            ? _formulaOriginCellRef
            : _workbook.ActiveSheet?.ActiveCellRef;
        if (targetSheet is null || cellRef is null) return;

        var cmd = new SetCellValueCommand(
            targetSheet,
            cellRef,
            value.StartsWith('=') ? null : value,
            value.StartsWith('=') ? value : null);
        _commandManager.Execute(cmd);
        InvalidateRenderedCells(new[] { cellRef });
        if (ReferenceEquals(targetSheet, _workbook.ActiveSheet))
            await SyncCanvasJsEngineCellsAsync(new[] { cellRef });
        if (_formulaOriginSheetIndex >= 0)
        {
            _formulaOriginSheetIndex = -1;
            _formulaOriginCellRef = null;
        }
    }

    // ── Toolbar commands ──
    private Task ApplyFontFamily(string? font)
    {
        return font is null
            ? Task.CompletedTask
            : ApplyStyleToSelectionAsync(s => s.FontFamily = font);
    }

    private Task ApplyFontSize(string? size)
    {
        if (size is null || !double.TryParse(size, out var value)) return Task.CompletedTask;
        return ApplyStyleToSelectionAsync(s => s.FontSize = value);
    }

    private Task ToggleBold()
    {
        return ApplyStyleToSelectionAsync(s => s.Bold = !s.Bold);
    }

    private Task ToggleItalic()
    {
        return ApplyStyleToSelectionAsync(s => s.Italic = !s.Italic);
    }

    private Task ToggleUnderline()
    {
        return ApplyStyleToSelectionAsync(s => s.Underline = !s.Underline);
    }

    private Task ToggleStrikeThrough()
    {
        return ApplyStyleToSelectionAsync(s => s.StrikeThrough = !s.StrikeThrough);
    }

    private Task IncreaseIndent()
    {
        return ApplyStyleToSelectionAsync(s => s.Indent = Math.Clamp(s.Indent + 1, 0, 15));
    }

    private Task DecreaseIndent()
    {
        return ApplyStyleToSelectionAsync(s => s.Indent = Math.Clamp(s.Indent - 1, 0, 15));
    }

    private void ShowFormatCellsDialog()
    {
        _formatCellsStyle = GetActiveCellStyle()?.Clone() ?? new SpreadsheetCellStyle();
        _showFormatCellsDialog = true;
    }

    private async Task OnFormatCellsApply(SpreadsheetCellStyle style)
    {
        var captured = style.Clone();
        await ApplyStyleToSelectionAsync(s =>
        {
            s.FontFamily = captured.FontFamily;
            s.FontSize = captured.FontSize;
            s.Bold = captured.Bold;
            s.Italic = captured.Italic;
            s.Underline = captured.Underline;
            s.DoubleUnderline = captured.DoubleUnderline;
            s.StrikeThrough = captured.StrikeThrough;
            s.Indent = captured.Indent;
            s.TextRotation = captured.TextRotation;
            s.ShrinkToFit = captured.ShrinkToFit;
            s.ForeColor = captured.ForeColor;
            s.BackgroundColor = captured.BackgroundColor;
            s.HorizontalAlign = captured.HorizontalAlign;
            s.VerticalAlign = captured.VerticalAlign;
            s.TextWrap = captured.TextWrap;
            s.NumberFormat = captured.NumberFormat;
            s.BorderTop = new SpreadsheetBorder(captured.BorderTop.Style, captured.BorderTop.Color);
            s.BorderRight = new SpreadsheetBorder(captured.BorderRight.Style, captured.BorderRight.Color);
            s.BorderBottom = new SpreadsheetBorder(captured.BorderBottom.Style, captured.BorderBottom.Color);
            s.BorderLeft = new SpreadsheetBorder(captured.BorderLeft.Style, captured.BorderLeft.Color);
        });
        _showFormatCellsDialog = false;
    }

    private void ShowFormatCellsDialogOnBorderTab()
    {
        _formatCellsStyle = GetActiveCellStyle()?.Clone() ?? new SpreadsheetCellStyle();
        _showFormatCellsDialog = true;
    }

    private void ActivateFormatPainter(bool sticky)
    {
        _formatPainterStyle = GetActiveCellStyle()?.Clone() ?? new SpreadsheetCellStyle();
        _formatPainterActive = true;
        _formatPainterSticky = sticky;
        StateHasChanged();
    }

    private void DeactivateFormatPainter()
    {
        _formatPainterActive = false;
        _formatPainterSticky = false;
        _formatPainterStyle = null;
        StateHasChanged();
    }

    private void HideRows((int Start, int End) range)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var indices = Enumerable.Range(range.Start, range.End - range.Start + 1);
        _commandManager.Execute(new HideRowsCommand(_workbook.ActiveSheet, indices, hidden: true));
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void UnhideRows((int Start, int End) range)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var start = Math.Max(0, range.Start);
        var end = Math.Min(_workbook.ActiveSheet.RowCount - 1, range.End);
        var hiddenRows = Enumerable.Range(start, end - start + 1)
            .Where(i => _workbook.ActiveSheet.Rows.TryGetValue(i, out var r) && r.IsHidden)
            .ToList();
        if (hiddenRows.Count == 0) return;
        _commandManager.Execute(new HideRowsCommand(_workbook.ActiveSheet, hiddenRows, hidden: false));
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void HideColumns((int Start, int End) range)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var indices = Enumerable.Range(range.Start, range.End - range.Start + 1);
        _commandManager.Execute(new HideColumnsCommand(_workbook.ActiveSheet, indices, hidden: true));
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void UnhideColumns((int Start, int End) range)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var start = Math.Max(0, range.Start);
        var end = Math.Min(_workbook.ActiveSheet.ColumnCount - 1, range.End);
        var hiddenCols = Enumerable.Range(start, end - start + 1)
            .Where(i => _workbook.ActiveSheet.Columns.TryGetValue(i, out var c) && c.IsHidden)
            .ToList();
        if (hiddenCols.Count == 0) return;
        _commandManager.Execute(new HideColumnsCommand(_workbook.ActiveSheet, hiddenCols, hidden: false));
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void ActivateFormatPainterFromContextMenu()
    {
        ActivateFormatPainter(sticky: false);
    }

    private async Task ClearFormatting()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        if (_grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        _commandManager.Execute(new SetCellStyleCommand(_workbook.ActiveSheet, refs, s =>
        {
            s.FontFamily = null;
            s.FontSize = 0;
            s.Bold = false;
            s.Italic = false;
            s.Underline = false;
            s.DoubleUnderline = false;
            s.StrikeThrough = false;
            s.Indent = 0;
            s.TextRotation = 0;
            s.ShrinkToFit = false;
            s.ForeColor = null;
            s.BackgroundColor = null;
            s.HorizontalAlign = SpreadsheetHorizontalAlign.General;
            s.VerticalAlign = SpreadsheetVerticalAlign.Bottom;
            s.TextWrap = false;
            s.NumberFormat = "General";
            s.BorderTop = new SpreadsheetBorder(SpreadsheetBorderStyle.None, null);
            s.BorderRight = new SpreadsheetBorder(SpreadsheetBorderStyle.None, null);
            s.BorderBottom = new SpreadsheetBorder(SpreadsheetBorderStyle.None, null);
            s.BorderLeft = new SpreadsheetBorder(SpreadsheetBorderStyle.None, null);
        }));
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);
        StateHasChanged();
    }

    private async Task ClearContent()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        if (_grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        _commandManager.Execute(new ClearCellContentCommand(_workbook.ActiveSheet, refs));
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);
        StateHasChanged();
    }

    private Task ClearAll()
    {
        return DeleteSelection();
    }

    private void OnFormatPainterButtonClick()
    {
        if (_formatPainterActive)
            DeactivateFormatPainter();
        else
            ActivateFormatPainter(sticky: false);
    }

    private void OnFormatPainterButtonDoubleClick()
    {
        ActivateFormatPainter(sticky: true);
    }

    private async Task OnFormatPainterApply(string cellRef)
    {
        if (_formatPainterStyle is null) return;
        var captured = _formatPainterStyle.Clone();
        await ApplyStyleToSelectionAsync(s =>
        {
            s.FontFamily = captured.FontFamily;
            s.FontSize = captured.FontSize;
            s.Bold = captured.Bold;
            s.Italic = captured.Italic;
            s.Underline = captured.Underline;
            s.DoubleUnderline = captured.DoubleUnderline;
            s.StrikeThrough = captured.StrikeThrough;
            s.Indent = captured.Indent;
            s.TextRotation = captured.TextRotation;
            s.ShrinkToFit = captured.ShrinkToFit;
            s.ForeColor = captured.ForeColor;
            s.BackgroundColor = captured.BackgroundColor;
            s.HorizontalAlign = captured.HorizontalAlign;
            s.VerticalAlign = captured.VerticalAlign;
            s.TextWrap = captured.TextWrap;
            s.NumberFormat = captured.NumberFormat;
            s.BorderTop = new SpreadsheetBorder(captured.BorderTop.Style, captured.BorderTop.Color);
            s.BorderRight = new SpreadsheetBorder(captured.BorderRight.Style, captured.BorderRight.Color);
            s.BorderBottom = new SpreadsheetBorder(captured.BorderBottom.Style, captured.BorderBottom.Color);
            s.BorderLeft = new SpreadsheetBorder(captured.BorderLeft.Style, captured.BorderLeft.Color);
        });
        if (!_formatPainterSticky)
            DeactivateFormatPainter();
    }

    private Task ApplyBorderPreset(BorderPreset preset)
    {
        var thin = SpreadsheetBorderStyle.Thin;
        var thick = SpreadsheetBorderStyle.Thick;
        var dbl = SpreadsheetBorderStyle.Double;
        const string black = "#000000";

        return ApplyStyleToSelectionAsync(s =>
        {
            static SpreadsheetBorder B(SpreadsheetBorderStyle st) => new(st, black);
            static SpreadsheetBorder None() => new(SpreadsheetBorderStyle.None, black);

            switch (preset)
            {
                case BorderPreset.None:
                    s.BorderTop = None(); s.BorderRight = None();
                    s.BorderBottom = None(); s.BorderLeft = None();
                    break;
                case BorderPreset.AllBorders:
                    s.BorderTop = B(thin); s.BorderRight = B(thin);
                    s.BorderBottom = B(thin); s.BorderLeft = B(thin);
                    break;
                case BorderPreset.OutsideBorders:
                    s.BorderTop = B(thin); s.BorderRight = B(thin);
                    s.BorderBottom = B(thin); s.BorderLeft = B(thin);
                    break;
                case BorderPreset.ThickBox:
                    s.BorderTop = B(thick); s.BorderRight = B(thick);
                    s.BorderBottom = B(thick); s.BorderLeft = B(thick);
                    break;
                case BorderPreset.BottomBorder:
                    s.BorderTop = None(); s.BorderRight = None();
                    s.BorderBottom = B(thin); s.BorderLeft = None();
                    break;
                case BorderPreset.ThickBottom:
                    s.BorderTop = None(); s.BorderRight = None();
                    s.BorderBottom = B(thick); s.BorderLeft = None();
                    break;
                case BorderPreset.DoubleBottom:
                    s.BorderTop = None(); s.BorderRight = None();
                    s.BorderBottom = B(dbl); s.BorderLeft = None();
                    break;
                case BorderPreset.TopBorder:
                    s.BorderTop = B(thin); s.BorderRight = None();
                    s.BorderBottom = None(); s.BorderLeft = None();
                    break;
                case BorderPreset.LeftBorder:
                    s.BorderTop = None(); s.BorderRight = None();
                    s.BorderBottom = None(); s.BorderLeft = B(thin);
                    break;
                case BorderPreset.RightBorder:
                    s.BorderTop = None(); s.BorderRight = B(thin);
                    s.BorderBottom = None(); s.BorderLeft = None();
                    break;
                case BorderPreset.TopAndThickBottom:
                    s.BorderTop = B(thin); s.BorderRight = None();
                    s.BorderBottom = B(thick); s.BorderLeft = None();
                    break;
            }
        });
    }

    private Task ApplyTextColor(string? color)
    {
        return ApplyStyleToSelectionAsync(s => s.ForeColor = string.IsNullOrEmpty(color) ? "#000000" : color);
    }

    private Task ApplyBackgroundColor(string? color)
    {
        return ApplyStyleToSelectionAsync(s => s.BackgroundColor = string.IsNullOrEmpty(color) ? "transparent" : color);
    }

    private Task ApplyAlign(string? align)
    {
        if (Enum.TryParse<SpreadsheetHorizontalAlign>(align, true, out var value))
        {
            return ApplyStyleToSelectionAsync(s => s.HorizontalAlign = value);
        }

        return Task.CompletedTask;
    }

    private Task ApplyNumberFormat(string? format)
    {
        if (format is null) return Task.CompletedTask;
        return ApplyStyleToSelectionAsync(s => s.NumberFormat = format);
    }

    private Task IncreaseDecimals()
    {
        return ApplyStyleToSelectionAsync(s =>
        {
            s.NumberFormat = AddDecimalPlace(s.NumberFormat);
        });
    }

    private Task DecreaseDecimals()
    {
        return ApplyStyleToSelectionAsync(s =>
        {
            s.NumberFormat = RemoveDecimalPlace(s.NumberFormat);
        });
    }

    private Task ApplyPercentageFormat()
    {
        return ApplyStyleToSelectionAsync(s => s.NumberFormat = "0%");
    }

    private Task ApplyThousandsFormat()
    {
        return ApplyStyleToSelectionAsync(s => s.NumberFormat = ToggleThousandsSeparator(s.NumberFormat));
    }

    private static string ToggleThousandsSeparator(string format)
    {
        if (format.Contains("#,#"))
            return RemoveThousandsSeparator(format);
        return AddThousandsSeparator(format);
    }

    private static string AddThousandsSeparator(string format)
    {
        if (format == "General" || format == "@") return "#,##0";
        // "0" → "#,##0", "0.00" → "#,##0.00", "0.000" → "#,##0.000"
        if (format.StartsWith('0'))
            return "#,##" + format;
        return "#,##0";
    }

    private static string RemoveThousandsSeparator(string format)
    {
        // "#,##0" → "0", "#,##0.00" → "0.00"
        if (format.StartsWith("#,##"))
            return format["#,##".Length..];
        return format.Replace(",", string.Empty);
    }

    private static string AddDecimalPlace(string format)
    {
        if (format == "General" || format == "@") return "0.0";
        var dotIndex = format.IndexOf('.');
        if (dotIndex < 0) return format + ".0";
        return format + "0";
    }

    private static string RemoveDecimalPlace(string format)
    {
        if (format == "General" || format == "@" || !format.Contains('.')) return format;
        var dotIndex = format.LastIndexOf('.');
        if (dotIndex < 0) return format;
        var afterDot = format[(dotIndex + 1)..];
        if (afterDot.Length <= 1) return format[..dotIndex];
        return format[..^(afterDot.Length - 1)];
    }

    private async Task ApplyStyleToSelectionAsync(Action<SpreadsheetCellStyle> mutate)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        if (_grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;

        await PreviewCanvasJsEngineStyleAsync(refs, mutate);
        var cmd = new SetCellStyleCommand(_workbook.ActiveSheet, refs, mutate);
        _commandManager.Execute(cmd);
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);
        StateHasChanged();
    }

    // ── Undo / Redo ──
    private void Undo()
    {
        _commandManager?.Undo();
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void Redo()
    {
        _commandManager?.Redo();
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void GridSelectAll()
    {
        _grid?.SelectAllCells();
    }

    // ── Clipboard ──
    private void Copy()
    {
        if (_workbook.ActiveSheet is null) return;
        if (_grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new CopyCommand(_workbook.ActiveSheet, refs);
        cmd.Execute();
    }

    private async Task Paste()
    {
        if (_workbook.ActiveSheet?.ActiveCellRef is null || _commandManager is null) return;
        var cmd = new PasteCommand(_workbook.ActiveSheet, _workbook.ActiveSheet.ActiveCellRef);
        _commandManager.Execute(cmd);
        InvalidateRenderedCells(cmd.AffectedCellRefs);
        await SyncCanvasJsEngineCellsAsync(cmd.AffectedCellRefs);
        StateHasChanged();
    }

    private async Task Cut()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        if (_grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new CutCommand(_workbook.ActiveSheet, refs);
        _commandManager.Execute(cmd);
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);
        StateHasChanged();
    }

    private void InsertRow()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (row, _) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new InsertRowCommand(_workbook.ActiveSheet, row));
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void DeleteRow()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (row, _) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new DeleteRowCommand(_workbook.ActiveSheet, row));
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void InsertColumn()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (_, col) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new InsertColumnCommand(_workbook.ActiveSheet, col));
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void DeleteColumn()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (_, col) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new DeleteColumnCommand(_workbook.ActiveSheet, col));
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private async Task DeleteSelection()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null || _grid is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new DeleteCellsCommand(_workbook.ActiveSheet, refs);
        _commandManager.Execute(cmd);
        InvalidateRenderedCells(refs);
        await SyncCanvasJsEngineCellsAsync(refs);
        StateHasChanged();
    }

    private static (int Row, int Col) ParseCellRef(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = SpreadsheetRange.ColumnLettersToIndex(letters);
        var row = int.TryParse(numbers, out var r) ? r - 1 : 0;
        return (row, col);
    }

    // ── Sheet tabs ──
    private void SwitchSheet(int index)
    {
        if (index < 0 || index >= _workbook.Sheets.Count) return;
        _workbook.ActiveSheetIndex = index;
        _commandManager = _workbook.ActiveSheet is not null
            ? new SpreadsheetCommandManager(_workbook.ActiveSheet)
            : null;
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void AddNewSheet()
    {
        var name = $"Sheet{_workbook.Sheets.Count + 1}";
        var cmd = new AddSheetCommand(_workbook, name);
        cmd.Execute();
        _workbook.ActiveSheetIndex = _workbook.Sheets.Count - 1;
        _commandManager = _workbook.ActiveSheet is not null
            ? new SpreadsheetCommandManager(_workbook.ActiveSheet)
            : null;
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void DeleteSheet(int index)
    {
        if (_workbook.Sheets.Count <= 1) return;
        var cmd = new DeleteSheetCommand(_workbook, index);
        cmd.Execute();
        _commandManager = _workbook.ActiveSheet is not null
            ? new SpreadsheetCommandManager(_workbook.ActiveSheet)
            : null;
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void RenameSheet((int Index, string NewName) args)
    {
        if (args.Index < 0 || args.Index >= _workbook.Sheets.Count) return;
        var sheet = _workbook.Sheets[args.Index];
        var cmd = new RenameSheetCommand(sheet, args.NewName);
        cmd.Execute();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    // ── Insert / View ──
    private void ShowInsertLinkDialog()
    {
        _insertLinkUrl = null;
        _insertLinkText = null;
        _showInsertLinkDialog = true;
    }

    private void OnLinkUrlInput(ChangeEventArgs e) => _insertLinkUrl = e.Value?.ToString();
    private void OnLinkTextInput(ChangeEventArgs e) => _insertLinkText = e.Value?.ToString();
    private void OnImageUrlInput(ChangeEventArgs e) => _insertImageUrl = e.Value?.ToString();

    private async Task ApplyInsertLink()
    {
        if (_workbook.ActiveSheet is null || string.IsNullOrWhiteSpace(_insertLinkUrl)) return;
        var cellRef = _workbook.ActiveSheet.ActiveCellRef ?? "A1";
        var cell = _workbook.ActiveSheet.Cells.GetValueOrDefault(cellRef) ?? new SpreadsheetCell();
        cell.Hyperlink = _insertLinkUrl.Trim();
        cell.Value = string.IsNullOrWhiteSpace(_insertLinkText) ? _insertLinkUrl.Trim() : _insertLinkText.Trim();
        _workbook.ActiveSheet.Cells[cellRef] = cell;
        InvalidateRenderedCells(new[] { cellRef });
        await SyncCanvasJsEngineCellsAsync(new[] { cellRef });
        _showInsertLinkDialog = false;
        StateHasChanged();
    }

    private void ShowInsertImageDialog()
    {
        _insertImageUrl = null;
        _showInsertImageDialog = true;
    }

    private async Task ApplyInsertImage()
    {
        if (_workbook.ActiveSheet is null || string.IsNullOrWhiteSpace(_insertImageUrl)) return;
        var cellRef = _workbook.ActiveSheet.ActiveCellRef ?? "A1";
        var cell = _workbook.ActiveSheet.Cells.GetValueOrDefault(cellRef) ?? new SpreadsheetCell();
        cell.ImageUrl = _insertImageUrl.Trim();
        _workbook.ActiveSheet.Cells[cellRef] = cell;
        InvalidateRenderedCells(new[] { cellRef });
        await SyncCanvasJsEngineCellsAsync(new[] { cellRef });
        _showInsertImageDialog = false;
        StateHasChanged();
    }

    private void ToggleMergeCells()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var bounds = GetSelectionBounds();
        if (bounds.StartRow == bounds.EndRow && bounds.StartCol == bounds.EndCol) return;

        var existing = _workbook.ActiveSheet.MergedCells.FirstOrDefault(r =>
            r.StartRow == bounds.StartRow && r.StartCol == bounds.StartCol
            && r.EndRow == bounds.EndRow && r.EndCol == bounds.EndCol);

        if (existing is not null)
        {
            _commandManager.Execute(new UnmergeCellsCommand(_workbook.ActiveSheet, bounds.StartRow, bounds.StartCol, bounds.EndRow, bounds.EndCol));
        }
        else
        {
            _commandManager.Execute(new MergeCellsCommand(_workbook.ActiveSheet, bounds.StartRow, bounds.StartCol, bounds.EndRow, bounds.EndCol));
        }
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void ToggleGridLines()
    {
        if (_workbook.ActiveSheet is null) return;
        _workbook.ActiveSheet.ShowGridLines = !_workbook.ActiveSheet.ShowGridLines;
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    // ── Open / Download ──
    private async Task TriggerFileInput()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('tm-spreadsheet-file-input').click()");
    }

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null || !file.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return;

        using var stream = file.OpenReadStream(maxAllowedSize: 10_000_000);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var data = ms.ToArray();

        var imported = XlsxImporter.Import(data);
        _workbook = imported;
        _commandManager = _workbook.ActiveSheet is not null ? new SpreadsheetCommandManager(_workbook.ActiveSheet) : null;

        await OnOpen.InvokeAsync(new SpreadsheetOpenEventArgs(file.Name, data, _workbook));
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private async Task DownloadXlsx()
    {
        var data = XlsxExporter.Export(_workbook);
        var fileName = $"{_workbook.ActiveSheet?.Name ?? "Spreadsheet"}.xlsx";
        var base64 = Convert.ToBase64String(data);
        var uri = $"data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{base64}";

        await JS.InvokeVoidAsync("eval", $"""
            var a = document.createElement('a');
            a.href = '{uri}';
            a.download = '{fileName}';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            """);

        await OnDownload.InvokeAsync(new SpreadsheetDownloadEventArgs(fileName, data));
    }

    // ── Public API ──

    /// <summary>Sets the value of a cell on the active sheet.</summary>
    public void SetCellValue(string cellRef, object? value)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var previous = _workbook.ActiveSheet.Cells.GetValueOrDefault(cellRef);
        var cmd = new SetCellValueCommand(_workbook.ActiveSheet, cellRef, value?.ToString(), null);
        _commandManager.Execute(cmd);
        _ = OnChange.InvokeAsync(new SpreadsheetChangeEventArgs(
            _workbook.ActiveSheet, cellRef, previous?.Value, value, previous?.Formula, null));
        _ = SyncCanvasJsEngineCellsAsync(new[] { cellRef });
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>Gets the value of a cell on the active sheet.</summary>
    public object? GetCellValue(string cellRef)
    {
        return _workbook.ActiveSheet?.Cells.GetValueOrDefault(cellRef)?.Value;
    }

    /// <summary>Gets the currently active sheet.</summary>
    public SpreadsheetSheet? GetActiveSheet() => _workbook.ActiveSheet;

    /// <summary>Exports the workbook to an XLSX byte array asynchronously.</summary>
    public Task<byte[]> ExportToExcelAsync()
    {
        return Task.FromResult(XlsxExporter.Export(_workbook));
    }

    /// <summary>Imports an XLSX byte array into the workbook asynchronously.</summary>
    public Task ImportFromExcelAsync(byte[] data)
    {
        var imported = XlsxImporter.Import(data);
        _workbook = imported;
        _commandManager = _workbook.ActiveSheet is not null ? new SpreadsheetCommandManager(_workbook.ActiveSheet) : null;
        RequestCanvasJsEngineFullRender();
        _ = InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    /// <summary>Focuses the grid element.</summary>
    public async Task FocusGridAsync()
    {
        if (_grid is not null)
            await _grid.FocusAsync();
    }

}
