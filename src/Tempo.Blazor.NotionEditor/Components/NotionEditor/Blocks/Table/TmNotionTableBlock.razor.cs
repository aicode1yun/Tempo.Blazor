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
    private const string ConflictTestId = "notion-table-conflict";

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
    private IReadOnlyList<double?> _columnWidths = [];
    private int              _dragSourceIndex = -1;
    private int              _dragOverIndex   = -1;
    private ElementReference _containerRef;
    private ElementReference _tableRef;
    private (int StartRow, int StartColumn, int EndRow, int EndColumn)? _selection;
    private (int Row, int Column)? _selectionAnchor;
    private bool _isSelecting;
    private bool _dragSelectionActivated;
    private readonly Stack<List<RowSnapshot>> _undoStack = new();
    private readonly Stack<List<RowSnapshot>> _redoStack = new();
    private bool _saving;
    private bool _hasConflict;
    private string? _saveError;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _hasHeaderRow     = Content?.HasHeaderRow    ?? false;
        _hasHeaderColumn  = Content?.HasHeaderColumn ?? false;
        _columnCount      = Content?.ColumnCount     ?? 0;
        _columnAlignments = Content?.ColumnAlignments ?? [];
        _columnWidths     = Content?.ColumnWidths ?? [];
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

    private bool CanAuthor =>
        !ReadOnly &&
        !_hasConflict &&
        Context.AggregateSession is not null;

    // ── Row loading ───────────────────────────────────────────────────────────

    private async Task LoadRowsAsync()
    {
        _loadingRows = true;
        StateHasChanged();
        try
        {
            if (Context.AggregateSession?.CurrentSnapshot is { } snapshot)
            {
                var view = NotionCanonicalTableBridge.ToView(snapshot, Block.Id);
                _rows = view.Rows;
                if (view.Table.Content is ITableBlockContent table)
                {
                    _hasHeaderRow = table.HasHeaderRow;
                    _hasHeaderColumn = table.HasHeaderColumn;
                    _columnCount = table.ColumnCount;
                    _columnAlignments = table.ColumnAlignments;
                    _columnWidths = table.ColumnWidths;
                }
            }
            else
            {
                var children = await Context.BlockProvider.GetChildBlocksAsync(Block.Id.ToString());
                _rows = children
                    .Where(b => b.Type == BlockType.TableRow)
                    .OrderBy(b => b.Order)
                    .ToList();
            }
            _rowsLoaded = true;
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
        }
        finally
        {
            _loadingRows = false;
            StateHasChanged();
        }
    }

    // ── Add row ───────────────────────────────────────────────────────────────

    private async Task AddRowAsync()
    {
        var cells = Enumerable.Range(0, _columnCount)
            .Select(_ => new NotionTableCell())
            .ToList();
        var newRow = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = Block.PageId,
            ParentBlockId = Block.Id,
            Type          = BlockType.TableRow,
            Order         = _rows.Count,
            Content       = BuildRowContent(cells)
        };
        try
        {
            var candidateRows = _rows.Append(newRow).ToList();
            if (await SaveAggregateTableAsync(Block, candidateRows))
                _rows = candidateRows;
            StateHasChanged();
        }
        catch (Exception ex) { _saveError = ex.Message; }
    }

    // ── Delete row ────────────────────────────────────────────────────────────

    private async Task DeleteRowAsync(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count) return;
        var row = _rows[rowIndex];
        try
        {
            var candidateRows = _rows.Where(candidate => candidate.Id != row.Id)
                .Select((candidate, index) =>
                    BuildRowBlock(candidate, (TableRowBlockContent)candidate.Content, index))
                .Cast<IPageBlock>()
                .ToList();
            if (await SaveAggregateTableAsync(Block, candidateRows))
                _rows = candidateRows;
            StateHasChanged();
        }
        catch (Exception ex) { _saveError = ex.Message; }
    }

    // ── Add column ────────────────────────────────────────────────────────────

    private async Task AddColumnAsync()
    {
        var newCount = _columnCount + 1;
        var candidateRows = _rows.Select(row =>
            row.Content is ITableRowBlockContent content
                ? (IPageBlock)BuildRowBlock(row, NotionTableEdit.AddColumn(content))
                : row).ToList();
        var candidateTable = BuildTableBlock(
            Block,
            NotionTableEdit.AddColumn(CurrentTableContent()));
        if (await SaveAggregateTableAsync(candidateTable, candidateRows))
        {
            _rows = candidateRows;
            _columnCount = newCount;
            ApplyTableView((ITableBlockContent)candidateTable.Content);
            await OnUpdated.InvokeAsync(candidateTable);
        }
        StateHasChanged();
    }

    // ── Delete column ─────────────────────────────────────────────────────────

    private async Task DeleteColumnAsync(int colIndex)
    {
        if (_columnCount <= 1 || colIndex < 0 || colIndex >= _columnCount) return;

        var newCount = _columnCount - 1;
        var grid = BuildGrid();
        RemoveGridColumn(grid, colIndex);
        var candidateRows = _rows.Select((row, index) =>
                (IPageBlock)BuildRowBlock(
                    row,
                    BuildRowContent(grid[index])))
            .ToList();
        var candidateTable = BuildTableBlock(
            Block,
            NotionTableEdit.RemoveColumn(CurrentTableContent(), colIndex));
        if (await SaveAggregateTableAsync(candidateTable, candidateRows))
        {
            _rows = candidateRows;
            _columnCount = newCount;
            ApplyTableView((ITableBlockContent)candidateTable.Content);
            await OnUpdated.InvokeAsync(candidateTable);
        }
        StateHasChanged();
    }

    // ── Header toggles ────────────────────────────────────────────────────────

    private async Task ToggleHeaderRowAsync()
    {
        var nextValue = !_hasHeaderRow;
        var updated = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = nextValue,
            HasHeaderColumn = _hasHeaderColumn,
            ColumnCount     = _columnCount,
            ColumnAlignments = _columnAlignments,
            ColumnWidths = _columnWidths
        });
        if (await SaveAggregateTableAsync(updated, _rows))
        {
            _hasHeaderRow = nextValue;
            await OnUpdated.InvokeAsync(updated);
        }
        StateHasChanged();
    }

    private async Task ToggleHeaderColumnAsync()
    {
        var nextValue = !_hasHeaderColumn;
        var updated = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = _hasHeaderRow,
            HasHeaderColumn = nextValue,
            ColumnCount     = _columnCount,
            ColumnAlignments = _columnAlignments,
            ColumnWidths = _columnWidths
        });
        if (await SaveAggregateTableAsync(updated, _rows))
        {
            _hasHeaderColumn = nextValue;
            await OnUpdated.InvokeAsync(updated);
        }
        StateHasChanged();
    }

    // ── Cell edit ─────────────────────────────────────────────────────────────

    private async Task HandleCellsChangedAsync(IPageBlock row, IReadOnlyList<NotionTableCell> cells)
    {
        var updated = BuildRowBlock(row, BuildRowContent(cells));
        var idx = _rows.FindIndex(r => r.Id == row.Id);
        try
        {
            var candidateRows = _rows.ToList();
            if (idx >= 0) candidateRows[idx] = updated;
            if (await SaveAggregateTableAsync(Block, candidateRows))
                _rows = candidateRows;
        }
        catch (Exception ex) { _saveError = ex.Message; }
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

        var undoSnapshot = CaptureRows();
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

        if (await PersistGridAsync(grid))
            PushUndoSnapshot(undoSnapshot);
    }

    private async Task SplitSelectionAsync()
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        var undoSnapshot = CaptureRows();
        var origins = GetSelectedOrigins(grid, selection);
        foreach (var (row, column) in origins)
            SplitOrigin(grid, row, column);

        if (await PersistGridAsync(grid))
            PushUndoSnapshot(undoSnapshot);
    }

    private async Task ApplySelectionColorAsync(string backgroundColor)
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        var undoSnapshot = CaptureRows();
        foreach (var (row, column) in GetSelectedOrigins(grid, selection))
            grid[row][column].BackgroundColor = backgroundColor;

        if (await PersistGridAsync(grid))
            PushUndoSnapshot(undoSnapshot);
    }

    private async Task ClearSelectionColorAsync()
    {
        if (_selection is not { } selection)
            return;

        var grid = BuildGrid();
        if (!SelectionWithinGrid(selection, grid))
            return;

        var undoSnapshot = CaptureRows();
        foreach (var (row, column) in GetSelectedOrigins(grid, selection))
            grid[row][column].BackgroundColor = null;

        if (await PersistGridAsync(grid))
            PushUndoSnapshot(undoSnapshot);
    }

    private async Task UndoTableChangeAsync()
    {
        if (_undoStack.Count == 0)
            return;

        _redoStack.Push(CaptureRows());
        var snapshot = _undoStack.Pop();
        var candidateRows = RestoreRows(snapshot);
        if (await SaveAggregateTableAsync(Block, candidateRows))
            _rows = candidateRows;
        else
        {
            _redoStack.Pop();
            _undoStack.Push(snapshot);
        }
        _selection = null;
        StateHasChanged();
    }

    private async Task RedoTableChangeAsync()
    {
        if (_redoStack.Count == 0)
            return;

        _undoStack.Push(CaptureRows());
        var snapshot = _redoStack.Pop();
        var candidateRows = RestoreRows(snapshot);
        if (await SaveAggregateTableAsync(Block, candidateRows))
            _rows = candidateRows;
        else
        {
            _undoStack.Pop();
            _redoStack.Push(snapshot);
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

        var candidateRows = headerRows.Concat(bodyRows)
            .Select((row, index) =>
                (IPageBlock)BuildRowBlock(
                    row,
                    (TableRowBlockContent)row.Content,
                    index))
            .ToList();
        var undoSnapshot = CaptureRows();
        if (await SaveAggregateTableAsync(Block, candidateRows))
        {
            _rows = candidateRows;
            PushUndoSnapshot(undoSnapshot);
        }
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

        var reorderedRows = _rows.ToList();
        var row = reorderedRows[src];
        reorderedRows.RemoveAt(src);
        var insertAt = src < tgt ? tgt - 1 : tgt;
        reorderedRows.Insert(Math.Max(0, Math.Min(insertAt, reorderedRows.Count)), row);

        var candidateRows = reorderedRows.Select((candidate, index) =>
                (IPageBlock)BuildRowBlock(
                    candidate,
                    (TableRowBlockContent)candidate.Content,
                    index))
            .ToList();
        var undoSnapshot = CaptureRows();
        if (await SaveAggregateTableAsync(Block, candidateRows))
        {
            _rows = candidateRows;
            PushUndoSnapshot(undoSnapshot);
        }

        StateHasChanged();
    }

    private Task HandleRowDragEndAsync()
    {
        _dragSourceIndex = -1;
        _dragOverIndex   = -1;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void PushUndoSnapshot(IReadOnlyList<RowSnapshot> snapshot)
    {
        _undoStack.Push(snapshot.ToList());
        _redoStack.Clear();
    }

    private List<RowSnapshot> CaptureRows()
        => _rows.Select(row =>
                new RowSnapshot(
                    row,
                    NormalizeRow(row).Select(cell => cell.Clone()).ToList()))
            .ToList();

    private static List<IPageBlock> RestoreRows(IReadOnlyList<RowSnapshot> snapshot)
        => snapshot.Select((item, index) =>
                (IPageBlock)BuildRowBlock(
                    item.Row,
                    BuildRowContent(item.Cells),
                    index))
            .ToList();

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

        return Enumerable.Range(0, _columnCount)
            .Select(_ => new NotionTableCell())
            .ToList();
    }

    private async Task<bool> PersistGridAsync(List<List<NotionTableCell>> grid)
    {
        if (!NotionTableGridValidator.TryValidate(grid, _columnCount, out _))
            return false;

        var candidateRows = _rows
            .Select((row, index) =>
                (IPageBlock)BuildRowBlock(
                    row,
                    BuildRowContent(grid[index]),
                    index))
            .ToList();
        var saved = await SaveAggregateTableAsync(Block, candidateRows);
        if (saved)
            _rows = candidateRows;
        StateHasChanged();
        return saved;
    }

    private static TableRowBlockContent BuildRowContent(IReadOnlyList<NotionTableCell> cells)
        => new()
        {
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

    private static void RemoveGridColumn(
        List<List<NotionTableCell>> grid,
        int columnIndex)
    {
        var affectedOrigins = new HashSet<(int Row, int Column)>();
        for (var row = 0; row < grid.Count; row++)
        {
            if (columnIndex < grid[row].Count)
                affectedOrigins.Add(GetOrigin(grid, row, columnIndex));
        }

        foreach (var (originRow, originColumn) in affectedOrigins)
        {
            if (originRow < 0 ||
                originRow >= grid.Count ||
                originColumn < 0 ||
                originColumn >= grid[originRow].Count)
            {
                continue;
            }

            var origin = grid[originRow][originColumn];
            if (origin.IsMergeHidden || origin.ColSpan <= 1)
                continue;

            if (originColumn == columnIndex)
            {
                var moved = origin.Clone();
                moved.ColSpan--;
                moved.IsMergeHidden = false;
                moved.MergeOriginRow = -1;
                moved.MergeOriginColumn = -1;
                grid[originRow][columnIndex + 1] = moved;
            }
            else
            {
                origin.ColSpan--;
            }
        }

        foreach (var row in grid)
        {
            if (columnIndex < row.Count)
                row.RemoveAt(columnIndex);
        }

        var origins = new List<(int Row, int Column, NotionTableCell Cell)>();
        for (var row = 0; row < grid.Count; row++)
        {
            for (var column = 0; column < grid[row].Count; column++)
            {
                var cell = grid[row][column];
                if (cell.IsMergeHidden)
                {
                    grid[row][column] = new NotionTableCell();
                }
                else
                {
                    origins.Add((row, column, cell));
                }
            }
        }

        foreach (var (originRow, originColumn, origin) in origins)
        {
            for (var row = originRow;
                 row < Math.Min(grid.Count, originRow + Math.Max(1, origin.RowSpan));
                 row++)
            {
                for (var column = originColumn;
                     column < Math.Min(grid[row].Count, originColumn + Math.Max(1, origin.ColSpan));
                     column++)
                {
                    if (row == originRow && column == originColumn)
                        continue;

                    grid[row][column] = new NotionTableCell
                    {
                        IsMergeHidden = true,
                        MergeOriginRow = originRow,
                        MergeOriginColumn = originColumn
                    };
                }
            }
        }
    }

    private string GetSortText(IPageBlock row, int column)
    {
        var cells = NormalizeRow(row);
        if (column < 0 || column >= cells.Count)
            return string.Empty;

        var cell = cells[column];
        var value = cell.Inlines.Count > 0
            ? string.Concat(cell.Inlines.Select(inline => inline.Text))
            : StripHtml(cell.Html);
        return WebUtility.HtmlDecode(value).Trim();
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
        ColumnAlignments = [.. _columnAlignments],
        ColumnWidths = [.. _columnWidths]
    };

    private static PageBlock BuildRowBlock(
        IPageBlock src,
        TableRowBlockContent content,
        int? order = null) => new()
    {
        Id            = src.Id,
        PageId        = src.PageId,
        ParentBlockId = src.ParentBlockId,
        Type          = src.Type,
        Order         = order ?? src.Order,
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

    private async Task<bool> SaveAggregateTableAsync(
        IPageBlock table,
        IReadOnlyList<IPageBlock> rows)
    {
        var session = Context.AggregateSession;
        if (session is null)
            return false;
        if (session.HasPendingConflict)
        {
            _hasConflict = true;
            return false;
        }

        _saving = true;
        _saveError = null;
        try
        {
            var result = await session.ApplyAsync(snapshot =>
                NotionCanonicalTableBridge.ReplaceTable(snapshot, table, rows));
            _hasConflict = result.Conflict;
            if (!result.Success && !result.Conflict)
            {
                _saveError = string.Join(
                    Environment.NewLine,
                    result.Issues.Select(issue => issue.Message));
            }
            return result.Success || result.Conflict;
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
            return false;
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task ReloadConflictAsync()
    {
        if (Context.AggregateSession is null)
            return;
        _saving = true;
        _saveError = null;
        try
        {
            var result = await Context.AggregateSession.ReloadAsync();
            if (result.Success && result.Snapshot is not null)
            {
                var view = NotionCanonicalTableBridge.ToView(result.Snapshot, Block.Id);
                ApplyAggregateView(view);
                await OnUpdated.InvokeAsync(view.Table);
                _hasConflict = false;
                _undoStack.Clear();
                _redoStack.Clear();
            }
            else
            {
                _saveError = FormatIssues(result.Issues);
            }
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
        }
        finally
        {
            _saving = false;
        }
        StateHasChanged();
    }

    private async Task ReapplyConflictAsync()
    {
        if (Context.AggregateSession is null)
            return;
        _saving = true;
        _saveError = null;
        try
        {
            var result = await Context.AggregateSession.ReapplyAsync();
            _hasConflict = Context.AggregateSession.HasPendingConflict;
            if (result.Success && result.Snapshot is not null)
            {
                var view = NotionCanonicalTableBridge.ToView(result.Snapshot, Block.Id);
                ApplyAggregateView(view);
                await OnUpdated.InvokeAsync(view.Table);
            }
            else
            {
                _saveError = FormatIssues(result.Issues);
            }
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
        }
        finally
        {
            _saving = false;
        }
        StateHasChanged();
    }

    private static string FormatIssues(IEnumerable<NotionAggregateIssue> issues)
        => string.Join(Environment.NewLine, issues.Select(issue => issue.Message));

    private void ApplyAggregateView((PageBlock Table, List<IPageBlock> Rows) view)
    {
        _rows = view.Rows;
        if (view.Table.Content is not ITableBlockContent table)
            return;

        ApplyTableView(table);
    }

    private void ApplyTableView(ITableBlockContent table)
    {
        _columnCount = table.ColumnCount;
        _hasHeaderRow = table.HasHeaderRow;
        _hasHeaderColumn = table.HasHeaderColumn;
        _columnAlignments = table.ColumnAlignments;
        _columnWidths = table.ColumnWidths;
    }

    private sealed record RowSnapshot(IPageBlock Row, IReadOnlyList<NotionTableCell> Cells);
}
