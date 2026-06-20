using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Phase 2 features: status-bar aggregation, zoom and find/replace.
/// </summary>
public partial class TmSpreadsheet
{
    private const double MinZoom = 0.5;
    private const double MaxZoom = 2.0;

    private double _zoom = 1.0;

    private bool _showFindReplace;
    private SpreadsheetSearchOptions _searchOptions = new();
    private IReadOnlyList<SpreadsheetSearchHit> _searchHits = [];
    private int _searchHitIndex = -1;

    /// <summary>The current zoom factor (1.0 = 100%).</summary>
    private double Zoom => _zoom;

    /// <summary>Aggregation for the current selection, shown in the status bar.</summary>
    private SpreadsheetAggregationResult StatusAggregation
    {
        get
        {
            var sheet = _workbook.ActiveSheet;
            if (sheet is null || _grid is null)
                return default;

            var values = _grid.GetSelectedCellRefs()
                .Select(cellRef => sheet.Cells.GetValueOrDefault(cellRef)?.Value);
            return SpreadsheetAggregation.Compute(values);
        }
    }

    // ── Zoom ──
    private Task OnZoomChanged(double zoom)
    {
        var clamped = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(clamped - _zoom) < 0.0001)
            return Task.CompletedTask;

        _zoom = clamped;
        RequestCanvasJsEngineFullRender();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OnZoomStep(int direction)
        => OnZoomChanged(Math.Round((_zoom + direction * 0.1) * 10) / 10);

    private Task ResetZoom() => OnZoomChanged(1.0);

    // ── Find / replace ──
    private int SearchMatchIndex => _searchHits.Count == 0 ? 0 : _searchHitIndex + 1;
    private int SearchMatchCount => _searchHits.Count;

    private void OpenFind()
    {
        _searchOptions ??= new SpreadsheetSearchOptions();
        _showFindReplace = true;
        StateHasChanged();
    }

    private void OpenReplace() => OpenFind();

    private async Task CloseFindReplace()
    {
        _showFindReplace = false;
        _searchHits = [];
        _searchHitIndex = -1;
        if (CanvasJsEngineGrid is not null)
            await CanvasJsEngineGrid.ClearEngineSearchHighlightsAsync();
        StateHasChanged();
    }

    private async Task OnSearchRequested(SpreadsheetSearchOptions options)
    {
        _searchOptions = options;
        await RunSearchAsync(navigate: true);
    }

    private async Task RunSearchAsync(bool navigate)
    {
        _searchHits = SpreadsheetSearchEngine.Find(
            _workbook, _workbook.ActiveSheetIndex, _searchOptions, CultureInfo.CurrentCulture);
        _searchHitIndex = _searchHits.Count > 0 ? 0 : -1;

        await ApplySearchHighlightsAsync();
        if (navigate)
            await NavigateToCurrentHitAsync();
        StateHasChanged();
    }

    private async Task FindNextHit()
    {
        if (_searchHits.Count == 0)
            return;
        _searchHitIndex = (_searchHitIndex + 1) % _searchHits.Count;
        await ApplySearchHighlightsAsync();
        await NavigateToCurrentHitAsync();
        StateHasChanged();
    }

    private async Task FindPreviousHit()
    {
        if (_searchHits.Count == 0)
            return;
        _searchHitIndex = (_searchHitIndex - 1 + _searchHits.Count) % _searchHits.Count;
        await ApplySearchHighlightsAsync();
        await NavigateToCurrentHitAsync();
        StateHasChanged();
    }

    private async Task NavigateToCurrentHitAsync()
    {
        if (_searchHitIndex < 0 || _searchHitIndex >= _searchHits.Count || _grid is null)
            return;

        var hit = _searchHits[_searchHitIndex];
        if (hit.SheetIndex != _workbook.ActiveSheetIndex)
            SwitchSheet(hit.SheetIndex);

        await _grid.NavigateToCellAsync(hit.CellRef);
    }

    private async Task ApplySearchHighlightsAsync()
    {
        if (CanvasJsEngineGrid is null)
            return;

        var activeRefs = _searchHits
            .Where(h => h.SheetIndex == _workbook.ActiveSheetIndex)
            .Select(h => h.CellRef)
            .ToArray();
        var current = _searchHitIndex >= 0 && _searchHitIndex < _searchHits.Count
            && _searchHits[_searchHitIndex].SheetIndex == _workbook.ActiveSheetIndex
            ? _searchHits[_searchHitIndex].CellRef
            : null;

        await CanvasJsEngineGrid.ApplyEngineSearchHighlightsAsync(activeRefs, current);
    }

    private async Task ReplaceCurrentHit(string replacement)
    {
        if (_searchHitIndex < 0 || _searchHitIndex >= _searchHits.Count)
            return;

        var hit = _searchHits[_searchHitIndex];
        if (hit.SheetIndex != _workbook.ActiveSheetIndex)
            SwitchSheet(hit.SheetIndex);
        if (_workbook.ActiveSheet is null || _commandManager is null)
            return;

        var cmd = new ReplaceCommand(_workbook.ActiveSheet, hit.CellRef, _searchOptions, replacement, CultureInfo.CurrentCulture);
        _commandManager.Execute(cmd);
        if (cmd.DidReplace)
        {
            InvalidateRenderedCells(new[] { hit.CellRef });
            await SyncCanvasJsEngineCellsAsync(new[] { hit.CellRef });
        }

        // Re-run the search; keep position near the replaced cell.
        var previousIndex = _searchHitIndex;
        await RunSearchAsync(navigate: false);
        if (_searchHits.Count > 0)
        {
            _searchHitIndex = Math.Min(previousIndex, _searchHits.Count - 1);
            await ApplySearchHighlightsAsync();
            await NavigateToCurrentHitAsync();
        }
        StateHasChanged();
    }

    private async Task ReplaceAllHits(string replacement)
    {
        if (_searchHits.Count == 0 || _commandManager is null)
            return;

        var culture = CultureInfo.CurrentCulture;
        var affected = new List<string>();

        // Group by sheet so each sheet's replacements form one undoable batch on its own manager.
        foreach (var group in _searchHits.GroupBy(h => h.SheetIndex))
        {
            if (group.Key != _workbook.ActiveSheetIndex)
                SwitchSheet(group.Key);
            if (_workbook.ActiveSheet is null || _commandManager is null)
                continue;

            var batch = new BatchCommand();
            foreach (var hit in group)
                batch.Add(new ReplaceCommand(_workbook.ActiveSheet, hit.CellRef, _searchOptions, replacement, culture, allInCell: true));
            _commandManager.Execute(batch);

            if (group.Key == _workbook.ActiveSheetIndex)
                affected.AddRange(group.Select(h => h.CellRef));
        }

        if (affected.Count > 0)
        {
            InvalidateRenderedCells(affected);
            await SyncCanvasJsEngineCellsAsync(affected);
        }

        await RunSearchAsync(navigate: false);
        StateHasChanged();
    }
}
