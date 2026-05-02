using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// An Excel-like spreadsheet component for viewing and editing tabular data.
/// Supports cell editing, formulas, styling, multi-sheet workbooks, and XLSX import/export.
/// </summary>
public partial class TmSpreadsheet
{
    private TmSpreadsheetGrid _grid = null!;
    private SpreadsheetWorkbook _workbook = new();
    private SpreadsheetCommandManager? _commandManager;
    private bool _isFormulaBarEditing;
    private string? _formulaBarEditValue;
    private bool _showInsertLinkDialog;
    private bool _showInsertImageDialog;
    private string? _insertLinkUrl;
    private string? _insertLinkText;
    private string? _insertImageUrl;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    private System.Threading.CancellationTokenSource? _onChangeDebounceCts;

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

    /// <summary>Additional CSS classes to apply to the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes to apply to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Gets the underlying workbook for programmatic access.</summary>
    public SpreadsheetWorkbook Workbook => _workbook;

    // ── Toolbar state ──
    private bool CanUndo => _commandManager?.CanUndo ?? false;
    private bool CanRedo => _commandManager?.CanRedo ?? false;

    private string? ToolbarFontFamily => GetActiveCellStyle()?.FontFamily;
    private string? ToolbarFontSize => GetActiveCellStyle()?.FontSize.ToString();
    private bool ToolbarIsBold => GetActiveCellStyle()?.Bold ?? false;
    private bool ToolbarIsItalic => GetActiveCellStyle()?.Italic ?? false;
    private bool ToolbarIsUnderline => GetActiveCellStyle()?.Underline ?? false;
    private string? ToolbarTextColor => GetActiveCellStyle()?.ForeColor;
    private string? ToolbarBackgroundColor => GetActiveCellStyle()?.BackgroundColor;
    private string? ToolbarAlign => GetActiveCellStyle()?.HorizontalAlign.ToString().ToLowerInvariant();
    private string? ToolbarNumberFormat => GetActiveCellStyle()?.NumberFormat ?? "General";
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
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        if (_workbook.ActiveSheet is not null && cellRef is not null)
        {
            _ = OnSelect.InvokeAsync(new SpreadsheetSelectEventArgs(
                _workbook.ActiveSheet,
                cellRef,
                _grid.SelectionStartRef,
                _grid.SelectionEndRef));
        }
        StateHasChanged();
    }

    private void OnGridCellEdit(SpreadsheetCellEditEventArgs args)
    {
        _ = OnCellEdit.InvokeAsync(args);
    }

    private void OnGridCellReferenceRequested(string cellRef)
    {
        // Insert cell reference into the current edit value (formula bar or inline)
        _grid.AppendEditValue(cellRef);
        if (_isFormulaBarEditing)
        {
            _formulaBarEditValue = (_formulaBarEditValue ?? string.Empty) + cellRef;
        }
        StateHasChanged();
    }

    private void OnGridCellValueCommitted((string CellRef, string? Value) args)
    {
        if (_commandManager is null || args.Value is null || _workbook.ActiveSheet is null) return;
        var previous = _workbook.ActiveSheet.Cells.GetValueOrDefault(args.CellRef);
        var cmd = new SetCellValueCommand(
            _workbook.ActiveSheet,
            args.CellRef,
            args.Value.StartsWith('=') ? null : args.Value,
            args.Value.StartsWith('=') ? args.Value : null);
        _commandManager.Execute(cmd);
        _ = OnChange.InvokeAsync(new SpreadsheetChangeEventArgs(
            _workbook.ActiveSheet,
            args.CellRef,
            previous?.Value,
            args.Value.StartsWith('=') ? null : args.Value,
            previous?.Formula,
            args.Value.StartsWith('=') ? args.Value : null));
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        StateHasChanged();
    }

    // ── Formula bar events ──
    private void OnFormulaBarEditStarted()
    {
        _isFormulaBarEditing = true;
        _formulaBarEditValue = GetActiveCellEditValue();
        // Also start editing in the grid so the cell shows input
        if (_workbook.ActiveSheet?.ActiveCellRef is { } cellRef)
        {
            // We can't directly call StartEdit because it's private,
            // but we can simulate the effect by setting the cell value
            // when committed. For visual sync the formula bar handles its own input.
        }
        StateHasChanged();
    }

    private void OnFormulaBarCommitted(string? value)
    {
        _isFormulaBarEditing = false;
        ApplyValueToActiveCell(value);
        _formulaBarEditValue = null;
        StateHasChanged();
    }

    private void OnFormulaBarCancelled()
    {
        _isFormulaBarEditing = false;
        _formulaBarEditValue = null;
        StateHasChanged();
    }

    private void OnFormulaBarValueChanged(string? value)
    {
        _formulaBarEditValue = value;
    }

    private async Task OnFormulaBarTabPressed()
    {
        await _grid.FocusAsync();
    }

    private void ApplyValueToActiveCell(string? value)
    {
        var cellRef = _workbook.ActiveSheet?.ActiveCellRef;
        if (cellRef is null || value is null || _commandManager is null) return;

        var cmd = new SetCellValueCommand(
            _workbook.ActiveSheet!,
            cellRef,
            value.StartsWith('=') ? null : value,
            value.StartsWith('=') ? value : null);
        _commandManager.Execute(cmd);
    }

    // ── Toolbar commands ──
    private void ApplyFontFamily(string? font)
    {
        if (font is null) return;
        ApplyStyleToSelection(s => s.FontFamily = font);
    }

    private void ApplyFontSize(string? size)
    {
        if (size is null || !double.TryParse(size, out var value)) return;
        ApplyStyleToSelection(s => s.FontSize = value);
    }

    private void ToggleBold()
    {
        ApplyStyleToSelection(s => s.Bold = !s.Bold);
    }

    private void ToggleItalic()
    {
        ApplyStyleToSelection(s => s.Italic = !s.Italic);
    }

    private void ToggleUnderline()
    {
        ApplyStyleToSelection(s => s.Underline = !s.Underline);
    }

    private void ShowTextColorPicker()
    {
        // Phase 4 will implement a real color picker popup.
        // For now toggle between black, red, blue as a demo.
        var current = GetActiveCellStyle()?.ForeColor ?? "#000000";
        var next = current switch
        {
            "#000000" => "#FF0000",
            "#FF0000" => "#0000FF",
            _ => "#000000"
        };
        ApplyStyleToSelection(s => s.ForeColor = next);
    }

    private void ShowBackgroundColorPicker()
    {
        var current = GetActiveCellStyle()?.BackgroundColor ?? "transparent";
        var next = current switch
        {
            "transparent" => "#FFFF00",
            "#FFFF00" => "#90EE90",
            "#90EE90" => "transparent",
            _ => "transparent"
        };
        ApplyStyleToSelection(s => s.BackgroundColor = next);
    }

    private void ApplyAlign(string? align)
    {
        if (Enum.TryParse<SpreadsheetHorizontalAlign>(align, true, out var value))
        {
            ApplyStyleToSelection(s => s.HorizontalAlign = value);
        }
    }

    private void ApplyNumberFormat(string? format)
    {
        if (format is null) return;
        ApplyStyleToSelection(s => s.NumberFormat = format);
    }

    private void IncreaseDecimals()
    {
        ApplyStyleToSelection(s =>
        {
            s.NumberFormat = AddDecimalPlace(s.NumberFormat);
        });
    }

    private void DecreaseDecimals()
    {
        ApplyStyleToSelection(s =>
        {
            s.NumberFormat = RemoveDecimalPlace(s.NumberFormat);
        });
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

    private void ApplyStyleToSelection(Action<SpreadsheetCellStyle> mutate)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new SetCellStyleCommand(_workbook.ActiveSheet, refs, mutate);
        _commandManager.Execute(cmd);
        StateHasChanged();
    }

    // ── Undo / Redo ──
    private void Undo()
    {
        _commandManager?.Undo();
        StateHasChanged();
    }

    private void Redo()
    {
        _commandManager?.Redo();
        StateHasChanged();
    }

    private void GridSelectAll()
    {
        _grid.SelectAllCells();
    }

    // ── Clipboard ──
    private void Copy()
    {
        if (_workbook.ActiveSheet is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new CopyCommand(_workbook.ActiveSheet, refs);
        cmd.Execute();
    }

    private void Paste()
    {
        if (_workbook.ActiveSheet?.ActiveCellRef is null || _commandManager is null) return;
        var cmd = new PasteCommand(_workbook.ActiveSheet, _workbook.ActiveSheet.ActiveCellRef);
        _commandManager.Execute(cmd);
        StateHasChanged();
    }

    private void Cut()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new CutCommand(_workbook.ActiveSheet, refs);
        _commandManager.Execute(cmd);
        StateHasChanged();
    }

    private void InsertRow()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (row, _) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new InsertRowCommand(_workbook.ActiveSheet, row));
        StateHasChanged();
    }

    private void DeleteRow()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (row, _) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new DeleteRowCommand(_workbook.ActiveSheet, row));
        StateHasChanged();
    }

    private void InsertColumn()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (_, col) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new InsertColumnCommand(_workbook.ActiveSheet, col));
        StateHasChanged();
    }

    private void DeleteColumn()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var (_, col) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        _commandManager.Execute(new DeleteColumnCommand(_workbook.ActiveSheet, col));
        StateHasChanged();
    }

    private void DeleteSelection()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null) return;
        var refs = _grid.GetSelectedCellRefs().ToList();
        if (refs.Count == 0) return;
        var cmd = new DeleteCellsCommand(_workbook.ActiveSheet, refs);
        _commandManager.Execute(cmd);
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
        StateHasChanged();
    }

    private void RenameSheet((int Index, string NewName) args)
    {
        if (args.Index < 0 || args.Index >= _workbook.Sheets.Count) return;
        var sheet = _workbook.Sheets[args.Index];
        var cmd = new RenameSheetCommand(sheet, args.NewName);
        cmd.Execute();
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

    private void ApplyInsertLink()
    {
        if (_workbook.ActiveSheet is null || string.IsNullOrWhiteSpace(_insertLinkUrl)) return;
        var cellRef = _workbook.ActiveSheet.ActiveCellRef ?? "A1";
        var cell = _workbook.ActiveSheet.Cells.GetValueOrDefault(cellRef) ?? new SpreadsheetCell();
        cell.Hyperlink = _insertLinkUrl.Trim();
        cell.Value = string.IsNullOrWhiteSpace(_insertLinkText) ? _insertLinkUrl.Trim() : _insertLinkText.Trim();
        _workbook.ActiveSheet.Cells[cellRef] = cell;
        _showInsertLinkDialog = false;
        StateHasChanged();
    }

    private void ShowInsertImageDialog()
    {
        _insertImageUrl = null;
        _showInsertImageDialog = true;
    }

    private void ApplyInsertImage()
    {
        if (_workbook.ActiveSheet is null || string.IsNullOrWhiteSpace(_insertImageUrl)) return;
        var cellRef = _workbook.ActiveSheet.ActiveCellRef ?? "A1";
        var cell = _workbook.ActiveSheet.Cells.GetValueOrDefault(cellRef) ?? new SpreadsheetCell();
        cell.ImageUrl = _insertImageUrl.Trim();
        _workbook.ActiveSheet.Cells[cellRef] = cell;
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
        StateHasChanged();
    }

    private void ToggleGridLines()
    {
        if (_workbook.ActiveSheet is null) return;
        _workbook.ActiveSheet.ShowGridLines = !_workbook.ActiveSheet.ShowGridLines;
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

