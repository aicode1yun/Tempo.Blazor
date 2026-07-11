using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net;
using System.Text.RegularExpressions;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using TableColumnAlignment = Tempo.Blazor.DocumentEditor.Models.TableColumnAlignment;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Table;

public partial class TmNotionTableBlock : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public ITableBlockContent? Content  { get; set; }
    [Parameter] public bool                ReadOnly { get; set; }

    [Parameter] public EventCallback<IPageBlock> OnUpdated { get; set; }
    [Parameter] public EventCallback             OnFocused { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private List<IPageBlock> _rows            = [];
    private bool             _loadingRows;
    private bool             _rowsLoaded;
    private bool             _hasHeaderRow;
    private bool             _hasHeaderColumn;
    private int              _columnCount;
    private IReadOnlyList<TableColumnAlignment> _columnAlignments = [];
    private int              _dragSourceIndex = -1;
    private int              _dragOverIndex   = -1;
    private ElementReference _containerRef;
    private ElementReference _tableRef;
    private (int StartRow, int StartColumn, int EndRow, int EndColumn)? _selection;
    private (int Row, int Column)? _selectionAnchor;
    private bool _isSelecting;
    private bool _dragSelectionActivated;
    private readonly Stack<List<RowSnapshot>> _undoStack = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _hasHeaderRow     = Content?.HasHeaderRow    ?? false;
        _hasHeaderColumn  = Content?.HasHeaderColumn ?? false;
        _columnCount      = Content?.ColumnCount     ?? 0;
        _columnAlignments = Content?.ColumnAlignments ?? [];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_rowsLoaded && !_loadingRows)
            await LoadRowsAsync();
    }

    public void SetTableSelection(int startRow, int startColumn, int endRow, int endColumn)
    {
        _selection = (
            Math.Min(startRow, endRow),
            Math.Min(startColumn, endColumn),
            Math.Max(startRow, endRow),
            Math.Max(startColumn, endColumn));
        StateHasChanged();
    }

    private bool HasRangeSelection =>
        _selection is { } selection &&
        (selection.StartRow != selection.EndRow || selection.StartColumn != selection.EndColumn);

    // ── Row loading ───────────────────────────────────────────────────────────

    private async Task LoadRowsAsync()
    {
        _loadingRows = true;
        StateHasChanged();
        try
        {
            var children = await Context.BlockProvider.GetChildBlocksAsync(Block.Id.ToString());
            _rows = children
                .Where(b => b.Type == BlockType.TableRow)
                .OrderBy(b => b.Order)
                .ToList();
            _rowsLoaded = true;
        }
        catch { }
        finally
        {
            _loadingRows = false;
            StateHasChanged();
        }
    }

    // ── Add row ───────────────────────────────────────────────────────────────

    private async Task AddRowAsync()
    {
        var cells = Enumerable.Range(0, _columnCount).Select(_ => string.Empty).ToList<string>();
        var newRow = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = Block.PageId,
            ParentBlockId = Block.Id,
            Type          = BlockType.TableRow,
            Order         = _rows.Count,
            Content       = new TableRowBlockContent { Cells = cells }
        };
        try
        {
            var created = await Context.BlockProvider.CreateBlockAsync(
                Block.PageId.ToString(),
                newRow,
                _rows.LastOrDefault()?.Id.ToString());
            _rows.Add(created);
            StateHasChanged();
        }
        catch { }
    }

    // ── Delete row ────────────────────────────────────────────────────────────

    private async Task DeleteRowAsync(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count) return;
        var row = _rows[rowIndex];
        try
        {
            await Context.BlockProvider.DeleteBlockAsync(row.Id.ToString());
            _rows.RemoveAt(rowIndex);
            await RenumberRowsAsync();
            StateHasChanged();
        }
        catch { }
    }

    // ── Add column ────────────────────────────────────────────────────────────

    private async Task AddColumnAsync()
    {
        var newCount = _columnCount + 1;

        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Content is not ITableRowBlockContent rc) continue;
            var updated = BuildRowBlock(_rows[i], NotionTableEdit.AddColumn(rc));
            try { await Context.BlockProvider.UpdateBlockAsync(updated); _rows[i] = updated; }
            catch { }
        }

        var updatedTable = BuildTableBlock(Block, NotionTableEdit.AddColumn(CurrentTableContent()));
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updatedTable);
            await OnUpdated.InvokeAsync(updatedTable);
            _columnCount = newCount;
            StateHasChanged();
        }
        catch { }
    }

    // ── Delete column ─────────────────────────────────────────────────────────

    private async Task DeleteColumnAsync(int colIndex)
    {
        if (_columnCount <= 1 || colIndex < 0 || colIndex >= _columnCount) return;

        var newCount = _columnCount - 1;

        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Content is not ITableRowBlockContent rc) continue;
            var updated = BuildRowBlock(_rows[i], NotionTableEdit.RemoveColumn(rc, colIndex));
            try { await Context.BlockProvider.UpdateBlockAsync(updated); _rows[i] = updated; }
            catch { }
        }

        var updatedTable = BuildTableBlock(Block, NotionTableEdit.RemoveColumn(CurrentTableContent(), colIndex));
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updatedTable);
            await OnUpdated.InvokeAsync(updatedTable);
            _columnCount = newCount;
            StateHasChanged();
        }
        catch { }
    }

    // ── Header toggles ────────────────────────────────────────────────────────

    private async Task ToggleHeaderRowAsync()
    {
        _hasHeaderRow = !_hasHeaderRow;
        var updated = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = _hasHeaderRow,
            HasHeaderColumn = _hasHeaderColumn,
            ColumnCount     = _columnCount
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
            StateHasChanged();
        }
        catch { }
    }

    private async Task ToggleHeaderColumnAsync()
    {
        _hasHeaderColumn = !_hasHeaderColumn;
        var updated = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = _hasHeaderRow,
            HasHeaderColumn = _hasHeaderColumn,
            ColumnCount     = _columnCount
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
            StateHasChanged();
        }
        catch { }
    }

    // ── Cell edit ─────────────────────────────────────────────────────────────

    private async Task HandleCellsChangedAsync(IPageBlock row, IReadOnlyList<NotionTableCell> cells)
    {
        var updated = BuildRowBlock(row, BuildRowContent(cells));
        var idx = _rows.FindIndex(r => r.Id == row.Id);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            if (idx >= 0) _rows[idx] = updated;
        }
        catch { }
    }

    // ── Selection and advanced table operations ──────────────────────────────

    private void HandleCellMouseDown(TableCellSelectionRequest request)
    {
        _selectionAnchor = (request.RowIndex, request.ColumnIndex);
        _isSelecting = true;
        _dragSelectionActivated = false;
    }

    private void HandleCellMouseEnter(TableCellSelectionRequest request)
    {
        if (!_isSelecting || _selectionAnchor is not { } anchor)
            return;

        if (anchor.Row == request.RowIndex && anchor.Column == request.ColumnIndex)
            return;

        _dragSelectionActivated = true;
        SetTableSelection(anchor.Row, anchor.Column, request.RowIndex, request.ColumnIndex);
    }

    private void HandleCellMouseUp(TableCellSelectionRequest request)
    {
        if (_isSelecting && _dragSelectionActivated && _selectionAnchor is { } anchor)
            SetTableSelection(anchor.Row, anchor.Column, request.RowIndex, request.ColumnIndex);
        else if (!_dragSelectionActivated)
            _selection = null;

        _isSelecting = false;
        _selectionAnchor = null;
        _dragSelectionActivated = false;
        StateHasChanged();
    }

    private bool IsCellSelected(int rowIndex, int columnIndex)
        => _selection is { } selection &&
           rowIndex >= selection.StartRow &&
           rowIndex <= selection.EndRow &&
           columnIndex >= selection.StartColumn &&
           columnIndex <= selection.EndColumn;

    private async Task MergeSelectionAsync()
    {
        if (!HasRangeSelection || _selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        PushUndoSnapshot();
        SplitIntersectingMergedCells(grid, selection);

        var origin = grid[selection.StartRow][selection.StartColumn];
        origin.ColSpan = selection.EndColumn - selection.StartColumn + 1;
        origin.RowSpan = selection.EndRow - selection.StartRow + 1;
        origin.IsMergeHidden = false;
        origin.MergeOriginRow = -1;
        origin.MergeOriginColumn = -1;

        for (var row = selection.StartRow; row <= selection.EndRow; row++)
        {
            for (var column = selection.StartColumn; column <= selection.EndColumn; column++)
            {
                if (row == selection.StartRow && column == selection.StartColumn)
                    continue;

                grid[row][column] = new NotionTableCell
                {
                    IsMergeHidden = true,
                    MergeOriginRow = selection.StartRow,
                    MergeOriginColumn = selection.StartColumn
                };
            }
        }

        await PersistGridAsync(grid);
    }

    private async Task SplitSelectionAsync()
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        PushUndoSnapshot();
        var origins = GetSelectedOrigins(grid, selection);
        foreach (var (row, column) in origins)
            SplitOrigin(grid, row, column);

        await PersistGridAsync(grid);
    }

    private async Task ApplySelectionColorAsync(string backgroundColor)
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        PushUndoSnapshot();
        foreach (var (row, column) in GetSelectedOrigins(grid, selection))
            grid[row][column].BackgroundColor = backgroundColor;

        await PersistGridAsync(grid);
    }

    private async Task ClearSelectionColorAsync()
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        PushUndoSnapshot();
        foreach (var (row, column) in GetSelectedOrigins(grid, selection))
            grid[row][column].BackgroundColor = null;

        await PersistGridAsync(grid);
    }

    private async Task UndoTableChangeAsync()
    {
        if (_undoStack.Count == 0)
            return;

        var snapshot = _undoStack.Pop();
        foreach (var item in snapshot)
        {
            var index = _rows.FindIndex(row => row.Id == item.Row.Id);
            if (index < 0)
                continue;

            var updated = BuildRowBlock(item.Row, BuildRowContent(item.Cells));
            await Context.BlockProvider.UpdateBlockAsync(updated);
            _rows[index] = updated;
        }

        _selection = null;
        StateHasChanged();
    }

    private async Task SortSelectedColumnAsync()
    {
        if (_rows.Count <= 1)
            return;

        var column = Math.Clamp(_selection?.StartColumn ?? 0, 0, Math.Max(0, _columnCount - 1));
        var headerRows = _hasHeaderRow ? _rows.Take(1).ToList() : [];
        var bodyRows = _rows.Skip(headerRows.Count)
            .Select((row, index) => new { Row = row, Index = index, Text = GetSortText(row, column) })
            .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Index)
            .Select(item => item.Row)
            .ToList();

        _rows = [.. headerRows, .. bodyRows];

        try
        {
            await Context.BlockProvider.ReorderBlocksAsync(Block.PageId.ToString(), _rows.Select(row => row.Id.ToString()));
        }
        catch { }

        StateHasChanged();
    }

    // ── Tab navigation ────────────────────────────────────────────────────────

    private async Task FocusCellAsync(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || colIndex < 0) return;
        try { await JS.InvokeVoidAsync("tmNotionEditor.tableFocusCell", _tableRef, rowIndex, colIndex); }
        catch { }
    }

    // ── Drag & drop rows ──────────────────────────────────────────────────────

    private Task HandleRowDragStartAsync(int rowIndex)
    {
        _dragSourceIndex = rowIndex;
        return Task.CompletedTask;
    }

    private Task HandleRowDragOverAsync(int rowIndex)
    {
        if (_dragOverIndex != rowIndex)
        {
            _dragOverIndex = rowIndex;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private async Task HandleRowDropAsync()
    {
        var src = _dragSourceIndex;
        var tgt = _dragOverIndex;
        _dragSourceIndex = -1;
        _dragOverIndex   = -1;

        if (src < 0 || tgt < 0 || src == tgt)
        {
            StateHasChanged();
            return;
        }

        var row      = _rows[src];
        _rows.RemoveAt(src);
        var insertAt = src < tgt ? tgt - 1 : tgt;
        _rows.Insert(Math.Max(0, Math.Min(insertAt, _rows.Count)), row);

        var orderedIds = _rows.Select(r => r.Id.ToString()).ToList();
        try { await Context.BlockProvider.ReorderBlocksAsync(Block.PageId.ToString(), orderedIds); }
        catch { }

        StateHasChanged();
    }

    private Task HandleRowDragEndAsync()
    {
        _dragSourceIndex = -1;
        _dragOverIndex   = -1;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(_rows
            .Select(row => new RowSnapshot(row, NormalizeRow(row).Select(cell => cell.Clone()).ToList()))
            .ToList());
    }

    private List<List<NotionTableCell>> BuildGrid()
        => _rows.Select(NormalizeRow).ToList();

    private List<NotionTableCell> NormalizeRow(IPageBlock row)
    {
        var content = row.Content as ITableRowBlockContent;
        if (content?.RichCells is { Count: > 0 })
        {
            var rich = content.RichCells.Select(cell => cell.Clone()).ToList();
            while (rich.Count < _columnCount)
                rich.Add(new NotionTableCell());
            return rich.Take(_columnCount).ToList();
        }

        var legacy = content?.Cells ?? [];
        return Enumerable.Range(0, _columnCount)
            .Select(index => new NotionTableCell
            {
                Html = index < legacy.Count ? legacy[index] : string.Empty
            })
            .ToList();
    }

    private async Task PersistGridAsync(List<List<NotionTableCell>> grid)
    {
        if (!NotionTableGridValidator.TryValidate(grid, _columnCount, out _))
            return;

        for (var index = 0; index < Math.Min(_rows.Count, grid.Count); index++)
        {
            var updated = BuildRowBlock(_rows[index], BuildRowContent(grid[index]));
            await Context.BlockProvider.UpdateBlockAsync(updated);
            _rows[index] = updated;
        }

        StateHasChanged();
    }

    private static TableRowBlockContent BuildRowContent(IReadOnlyList<NotionTableCell> cells)
        => new()
        {
            Cells = cells.Select(cell => cell.IsMergeHidden ? string.Empty : cell.Html).ToList(),
            RichCells = cells.Select(cell => cell.Clone()).ToList()
        };

    private static bool SelectionWithinGrid(
        (int StartRow, int StartColumn, int EndRow, int EndColumn) selection,
        IReadOnlyList<IReadOnlyList<NotionTableCell>> grid)
        => selection.StartRow >= 0 &&
           selection.StartColumn >= 0 &&
           selection.EndRow < grid.Count &&
           grid.Count > 0 &&
           selection.EndColumn < grid[selection.StartRow].Count;

    private static void SplitIntersectingMergedCells(
        List<List<NotionTableCell>> grid,
        (int StartRow, int StartColumn, int EndRow, int EndColumn) selection)
    {
        var origins = GetSelectedOrigins(grid, selection);
        foreach (var (row, column) in origins)
            SplitOrigin(grid, row, column);
    }

    private static IReadOnlyList<(int Row, int Column)> GetSelectedOrigins(
        List<List<NotionTableCell>> grid,
        (int StartRow, int StartColumn, int EndRow, int EndColumn) selection)
    {
        var origins = new HashSet<(int Row, int Column)>();
        for (var row = selection.StartRow; row <= selection.EndRow; row++)
        {
            for (var column = selection.StartColumn; column <= selection.EndColumn; column++)
            {
                var origin = GetOrigin(grid, row, column);
                origins.Add(origin);
            }
        }

        return origins.ToList();
    }

    private static (int Row, int Column) GetOrigin(List<List<NotionTableCell>> grid, int row, int column)
    {
        var cell = grid[row][column];
        if (!cell.IsMergeHidden)
            return (row, column);

        if (cell.MergeOriginRow >= 0 &&
            cell.MergeOriginRow < grid.Count &&
            cell.MergeOriginColumn >= 0 &&
            cell.MergeOriginColumn < grid[cell.MergeOriginRow].Count)
            return (cell.MergeOriginRow, cell.MergeOriginColumn);

        return (row, column);
    }

    private static void SplitOrigin(List<List<NotionTableCell>> grid, int row, int column)
    {
        var origin = grid[row][column];
        var rowSpan = Math.Max(1, origin.RowSpan);
        var colSpan = Math.Max(1, origin.ColSpan);
        origin.RowSpan = 1;
        origin.ColSpan = 1;
        origin.IsMergeHidden = false;
        origin.MergeOriginRow = -1;
        origin.MergeOriginColumn = -1;

        for (var r = row; r < Math.Min(grid.Count, row + rowSpan); r++)
        {
            for (var c = column; c < Math.Min(grid[r].Count, column + colSpan); c++)
            {
                if (r == row && c == column)
                    continue;

                if (grid[r][c].IsMergeHidden &&
                    grid[r][c].MergeOriginRow == row &&
                    grid[r][c].MergeOriginColumn == column)
                    grid[r][c] = new NotionTableCell();
            }
        }
    }

    private string GetSortText(IPageBlock row, int column)
    {
        var cells = NormalizeRow(row);
        if (column < 0 || column >= cells.Count)
            return string.Empty;

        return WebUtility.HtmlDecode(StripHtml(cells[column].Html)).Trim();
    }

    private static string StripHtml(string html)
        => Regex.Replace(html, "<.*?>", string.Empty, RegexOptions.Singleline);

    // ── Block builders ────────────────────────────────────────────────────────

    /// <summary>The table's content as the component currently sees it, alignments included.</summary>
    private TableBlockContent CurrentTableContent() => new()
    {
        HasHeaderRow     = _hasHeaderRow,
        HasHeaderColumn  = _hasHeaderColumn,
        ColumnCount      = _columnCount,
        ColumnAlignments = [.. _columnAlignments]
    };

    /// <summary>
    /// Rows are addressed by Order. After a delete the remaining rows must close the gap, otherwise
    /// a later insert lands on a duplicate order and the rows come back in a different sequence.
    /// </summary>
    private async Task RenumberRowsAsync()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Order == i) continue;

            var renumbered = new PageBlock
            {
                Id            = _rows[i].Id,
                PageId        = _rows[i].PageId,
                ParentBlockId = _rows[i].ParentBlockId,
                Type          = _rows[i].Type,
                Order         = i,
                Content       = _rows[i].Content,
                CreatedAt     = _rows[i].CreatedAt,
                LastEditedAt  = DateTime.UtcNow
            };

            try { await Context.BlockProvider.UpdateBlockAsync(renumbered); _rows[i] = renumbered; }
            catch { }
        }
    }

    private static PageBlock BuildRowBlock(IPageBlock src, TableRowBlockContent content) => new()
    {
        Id            = src.Id,
        PageId        = src.PageId,
        ParentBlockId = src.ParentBlockId,
        Type          = src.Type,
        Order         = src.Order,
        Content       = content,
        CreatedAt     = src.CreatedAt,
        LastEditedAt  = DateTime.UtcNow
    };

    private static PageBlock BuildTableBlock(IPageBlock src, TableBlockContent content) => new()
    {
        Id            = src.Id,
        PageId        = src.PageId,
        ParentBlockId = src.ParentBlockId,
        Type          = src.Type,
        Order         = src.Order,
        Content       = content,
        CreatedAt     = src.CreatedAt,
        LastEditedAt  = DateTime.UtcNow
    };

    private sealed record RowSnapshot(IPageBlock Row, IReadOnlyList<NotionTableCell> Cells);
}
