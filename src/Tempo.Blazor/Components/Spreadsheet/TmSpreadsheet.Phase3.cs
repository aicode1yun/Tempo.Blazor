using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Phase 3 — AutoFilter + Sort wiring: toolbar Data tab actions, header filter-button dropdown,
/// custom filter dialog, and the multi-level sort dialog. Filter/sort operations run through the
/// command manager so they participate in undo/redo.
/// </summary>
public partial class TmSpreadsheet
{
    private int? _filterDropdownColumn;
    private double _filterDropdownX;
    private double _filterDropdownY;

    private bool _showCustomFilterDialog;
    private int? _customFilterColumn;
    private SpreadsheetFilterKind _customFilterKind = SpreadsheetFilterKind.Text;

    private bool _showSortDialog;
    private SpreadsheetRange? _sortDialogRange;

    private CultureInfo SpreadsheetCulture => CultureInfo.CurrentCulture;

    /// <summary>Whether the active sheet currently has an auto-filter enabled.</summary>
    private bool IsAutoFilterActive => _workbook.ActiveSheet?.AutoFilter is not null;

    // ── Toolbar: Data tab ──

    private void ToggleAutoFilter()
    {
        if (_workbook.ActiveSheet is null || _commandManager is null)
            return;

        if (_workbook.ActiveSheet.AutoFilter is not null)
        {
            _commandManager.Execute(new ClearAutoFilterCommand(_workbook.ActiveSheet));
        }
        else
        {
            var region = ComputeDataRegion();
            _commandManager.Execute(new SetAutoFilterCommand(_workbook.ActiveSheet, region));
        }

        ClearRenderedCache();
        InvalidateCanvasGeometry();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void SortSelectionAscending() => QuickSort(SpreadsheetSortDirection.Ascending);

    private void SortSelectionDescending() => QuickSort(SpreadsheetSortDirection.Descending);

    private void QuickSort(SpreadsheetSortDirection direction)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null)
            return;

        var region = _workbook.ActiveSheet.AutoFilter?.Range ?? ComputeDataRegion();
        var (_, col) = ParseCellRef(_workbook.ActiveSheet.ActiveCellRef ?? "A1");
        col = Math.Clamp(col, region.StartCol, region.EndCol);

        var spec = new SpreadsheetSortSpec(region)
        {
            HasHeader = _workbook.ActiveSheet.AutoFilter is not null || DetectHasHeader(region),
            Levels = [new SpreadsheetSortLevel { KeyIndex = col, Direction = direction }]
        };

        ExecuteSort(spec);
    }

    private void OpenSortDialog()
    {
        if (_workbook.ActiveSheet is null)
            return;

        _sortDialogRange = _workbook.ActiveSheet.AutoFilter?.Range ?? ComputeDataRegion();
        _showSortDialog = true;
        StateHasChanged();
    }

    private void ApplySortSpec(SpreadsheetSortSpec spec)
    {
        _showSortDialog = false;
        ExecuteSort(spec);
    }

    private void ExecuteSort(SpreadsheetSortSpec spec)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null)
            return;

        var command = new SortRangeCommand(_workbook.ActiveSheet, spec, SpreadsheetCulture);
        if (command.HasMergeConflict)
            return;

        _commandManager.Execute(command);
        ReapplyAutoFilterHidden();
        ClearRenderedCache();
        InvalidateCanvasGeometry();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    // ── Header filter button → dropdown ──

    private void OnFilterButtonClicked(TmSpreadsheetCanvasGrid.CanvasFilterButtonClick click)
    {
        _filterDropdownColumn = click.Column;
        _filterDropdownX = click.ClientX;
        _filterDropdownY = click.ClientY;
        _showCustomFilterDialog = false;
        StateHasChanged();
    }

    private void CloseFilterDropdown()
    {
        _filterDropdownColumn = null;
        StateHasChanged();
    }

    private void ApplyColumnFilter(SpreadsheetColumnFilter? columnFilter)
    {
        if (_workbook.ActiveSheet is null || _commandManager is null || _workbook.ActiveSheet.AutoFilter is null)
            return;

        var column = columnFilter?.ColumnIndex
            ?? _filterDropdownColumn
            ?? _customFilterColumn
            ?? 0;

        var command = columnFilter is null
            ? new UpdateColumnFilterCommand(_workbook.ActiveSheet, column, SpreadsheetCulture)
            : new UpdateColumnFilterCommand(_workbook.ActiveSheet, columnFilter, SpreadsheetCulture);

        _commandManager.Execute(command);

        _filterDropdownColumn = null;
        _showCustomFilterDialog = false;
        ClearRenderedCache();
        InvalidateCanvasGeometry();
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
    }

    private void OpenCustomFilterDialog(int column)
    {
        _customFilterColumn = column;
        _customFilterKind = DetectColumnKind(column);
        _filterDropdownColumn = null;
        _showCustomFilterDialog = true;
        StateHasChanged();
    }

    private void SortColumnAscending(int column) => SortByColumn(column, SpreadsheetSortDirection.Ascending);

    private void SortColumnDescending(int column) => SortByColumn(column, SpreadsheetSortDirection.Descending);

    private void SortByColumn(int column, SpreadsheetSortDirection direction)
    {
        _filterDropdownColumn = null;
        if (_workbook.ActiveSheet?.AutoFilter is not { } filter)
            return;

        var spec = new SpreadsheetSortSpec(filter.Range)
        {
            HasHeader = true,
            Levels = [new SpreadsheetSortLevel { KeyIndex = column, Direction = direction }]
        };

        ExecuteSort(spec);
    }

    // ── Helpers ──

    private void ReapplyAutoFilterHidden()
    {
        var sheet = _workbook.ActiveSheet;
        if (sheet?.AutoFilter is not { } filter)
            return;

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, SpreadsheetCulture).ToHashSet();
        for (var row = filter.FirstDataRow; row <= filter.Range.EndRow; row++)
        {
            if (!sheet.Rows.TryGetValue(row, out var meta))
            {
                if (!hidden.Contains(row))
                    continue;
                meta = new SpreadsheetRow { Index = row };
                sheet.Rows[row] = meta;
            }

            meta.IsHidden = hidden.Contains(row);
        }
    }

    private SpreadsheetFilterKind DetectColumnKind(int column)
    {
        var sheet = _workbook.ActiveSheet;
        if (sheet is null)
            return SpreadsheetFilterKind.Text;

        var region = sheet.AutoFilter?.Range ?? ComputeDataRegion();
        var firstDataRow = region.StartRow + 1;
        for (var row = firstDataRow; row <= region.EndRow; row++)
        {
            switch (sheet.GetCell(row, column)?.DataType)
            {
                case SpreadsheetDataType.Number or SpreadsheetDataType.Currency or SpreadsheetDataType.Percentage:
                    return SpreadsheetFilterKind.Number;
                case SpreadsheetDataType.Date or SpreadsheetDataType.DateTime or SpreadsheetDataType.Time:
                    return SpreadsheetFilterKind.Date;
            }
        }

        return SpreadsheetFilterKind.Text;
    }

    private bool DetectHasHeader(SpreadsheetRange region)
    {
        var sheet = _workbook.ActiveSheet;
        if (sheet is null || region.RowCount < 2)
            return false;

        var headerAllText = Enumerable.Range(region.StartCol, region.ColumnCount)
            .Select(c => sheet.GetCell(region.StartRow, c))
            .Where(c => c?.Value is not null)
            .All(c => c!.DataType == SpreadsheetDataType.Text);

        var bodyHasNonText = false;
        for (var row = region.StartRow + 1; row <= region.EndRow && !bodyHasNonText; row++)
            for (var c = region.StartCol; c <= region.EndCol; c++)
                if (sheet.GetCell(row, c)?.DataType is { } t && t != SpreadsheetDataType.Text)
                {
                    bodyHasNonText = true;
                    break;
                }

        return headerAllText && bodyHasNonText;
    }

    /// <summary>
    /// Determines the contiguous data region to filter/sort. When the selection spans multiple cells
    /// that range is used; otherwise the current region around the active cell is grown outward across
    /// non-empty rows and columns (Excel's Ctrl+* behaviour).
    /// </summary>
    private SpreadsheetRange ComputeDataRegion()
    {
        var sheet = _workbook.ActiveSheet!;
        var bounds = GetSelectionBounds();
        if (bounds.StartRow != bounds.EndRow || bounds.StartCol != bounds.EndCol)
            return new SpreadsheetRange(bounds.StartRow, bounds.StartCol, bounds.EndRow, bounds.EndCol);

        var (row, col) = (bounds.StartRow, bounds.StartCol);
        if (!CellHasContent(sheet, row, col))
            return new SpreadsheetRange(row, col, row, col);

        var top = row;
        while (top > 0 && RowHasContent(sheet, top - 1, col)) top--;
        var bottom = row;
        while (bottom < sheet.RowCount - 1 && RowHasContent(sheet, bottom + 1, col)) bottom++;

        var left = col;
        while (left > 0 && ColumnHasContent(sheet, top, bottom, left - 1)) left--;
        var right = col;
        while (right < sheet.ColumnCount - 1 && ColumnHasContent(sheet, top, bottom, right + 1)) right++;

        return new SpreadsheetRange(top, left, bottom, right);
    }

    private static bool CellHasContent(SpreadsheetSheet sheet, int row, int col)
    {
        var cell = sheet.GetCell(row, col);
        return cell?.Value is not null || !string.IsNullOrEmpty(cell?.Formula);
    }

    private static bool RowHasContent(SpreadsheetSheet sheet, int row, int anchorCol)
    {
        // A row is part of the region when its anchor column or an immediate neighbour has content.
        for (var c = Math.Max(0, anchorCol - 1); c <= anchorCol + 1 && c < sheet.ColumnCount; c++)
            if (CellHasContent(sheet, row, c))
                return true;
        return false;
    }

    private static bool ColumnHasContent(SpreadsheetSheet sheet, int top, int bottom, int col)
    {
        for (var r = top; r <= bottom; r++)
            if (CellHasContent(sheet, r, col))
                return true;
        return false;
    }
}
