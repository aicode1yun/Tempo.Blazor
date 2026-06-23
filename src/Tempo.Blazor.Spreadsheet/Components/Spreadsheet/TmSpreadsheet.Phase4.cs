using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Phase 4 — data tools wiring: Remove Duplicates, Text to Columns and Paste Special. Each opens its
/// dialog from the Data tab (Paste Special also via Ctrl+Shift+V), then runs an atomic command through
/// the command manager so the operation participates in undo/redo. Remove Duplicates surfaces a
/// localized, plural-aware result banner.
/// </summary>
public partial class TmSpreadsheet
{
    // ── Remove duplicates ──
    private bool _showRemoveDuplicatesDialog;
    private SpreadsheetRange? _dedupRange;
    private Dictionary<int, string>? _dedupHeaderLabels;
    private string? _dataToolMessage;

    // ── Text to columns ──
    private bool _showTextToColumnsDialog;
    private List<string> _t2cSourceRows = [];
    private int _t2cSourceCol;
    private int _t2cStartRow;
    private int _t2cEndRow;

    // ── Paste special ──
    private bool _showPasteSpecialDialog;

    private void OpenRemoveDuplicatesDialog()
    {
        if (_workbook.ActiveSheet is null)
            return;

        _dedupRange = _workbook.ActiveSheet.AutoFilter?.Range ?? ComputeDataRegion();
        _dedupHeaderLabels = BuildHeaderLabels(_dedupRange);
        _showRemoveDuplicatesDialog = true;
        StateHasChanged();
    }

    private void ApplyRemoveDuplicates(SpreadsheetRemoveDuplicatesOptions options)
    {
        _showRemoveDuplicatesDialog = false;
        if (_workbook.ActiveSheet is null || _commandManager is null || _dedupRange is null)
            return;

        var command = new RemoveDuplicatesCommand(
            _workbook.ActiveSheet, _dedupRange, options.KeyColumns, options.HasHeader, options.CaseSensitive, SpreadsheetCulture);
        if (command.HasMergeConflict)
            return;

        _commandManager.Execute(command);
        ReapplyAutoFilterHidden();
        ClearRenderedCache();
        InvalidateCanvasGeometry();
        RequestCanvasJsEngineFullRender();

        _dataToolMessage = command.RemovedCount == 0
            ? Loc["TmSpreadsheet_Dedup_ResultNone"]
            : string.Format(Loc["TmSpreadsheet_Dedup_Result"], command.RemovedCount, command.RemainingCount);

        StateHasChanged();
    }

    private void DismissDataToolMessage()
    {
        _dataToolMessage = null;
        StateHasChanged();
    }

    private Dictionary<int, string> BuildHeaderLabels(SpreadsheetRange region)
    {
        var sheet = _workbook.ActiveSheet!;
        var labels = new Dictionary<int, string>();
        for (var col = region.StartCol; col <= region.EndCol; col++)
        {
            var cell = sheet.GetCell(region.StartRow, col);
            var text = cell?.Value as string ?? cell?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                labels[col] = text;
        }
        return labels;
    }

    // ── Text to columns ──

    private void OpenTextToColumnsDialog()
    {
        if (_workbook.ActiveSheet is null)
            return;

        var bounds = GetSelectionBounds();
        _t2cSourceCol = bounds.StartCol;

        if (bounds.StartRow == bounds.EndRow)
        {
            // Single cell: grow over the contiguous data region of that column.
            var region = ComputeDataRegion();
            _t2cStartRow = region.StartRow;
            _t2cEndRow = region.EndRow;
        }
        else
        {
            _t2cStartRow = bounds.StartRow;
            _t2cEndRow = bounds.EndRow;
        }

        _t2cSourceRows = ReadColumnText(_t2cSourceCol, _t2cStartRow, _t2cEndRow);
        _showTextToColumnsDialog = true;
        StateHasChanged();
    }

    private void ApplyTextToColumns(SpreadsheetTextToColumnsResult result)
    {
        _showTextToColumnsDialog = false;
        if (_workbook.ActiveSheet is null || _commandManager is null)
            return;

        var command = new TextToColumnsCommand(
            _workbook.ActiveSheet, _t2cSourceCol, _t2cStartRow, _t2cEndRow, result.Options, result.Formats, SpreadsheetCulture);
        _commandManager.Execute(command);

        ClearRenderedCache();
        InvalidateCanvasGeometry();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private List<string> ReadColumnText(int col, int startRow, int endRow)
    {
        var sheet = _workbook.ActiveSheet!;
        var rows = new List<string>();
        for (var row = startRow; row <= endRow; row++)
        {
            var cell = sheet.GetCell(row, col);
            rows.Add(cell?.Value as string ?? cell?.Value?.ToString() ?? string.Empty);
        }
        return rows;
    }

    // ── Paste special ──

    private void OpenPasteSpecialDialog()
    {
        if (SpreadsheetClipboard.Cells is null || SpreadsheetClipboard.Cells.Count == 0)
            return;

        _showPasteSpecialDialog = true;
        StateHasChanged();
    }

    private async Task ApplyPasteSpecial(Tempo.Blazor.Components.Spreadsheet.Data.SpreadsheetPasteSpecialOptions options)
    {
        _showPasteSpecialDialog = false;
        if (_workbook.ActiveSheet?.ActiveCellRef is null || _commandManager is null)
            return;

        var command = new PasteSpecialCommand(_workbook.ActiveSheet, _workbook.ActiveSheet.ActiveCellRef, options, SpreadsheetCulture);
        _commandManager.Execute(command);

        InvalidateRenderedCells(command.AffectedCellRefs);
        await SyncCanvasJsEngineCellsAsync(command.AffectedCellRefs);
        ClearRenderedCache();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }
}
