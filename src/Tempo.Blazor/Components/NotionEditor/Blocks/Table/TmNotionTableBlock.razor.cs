using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

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
    private int              _dragSourceIndex = -1;
    private int              _dragOverIndex   = -1;
    private ElementReference _containerRef;
    private ElementReference _tableRef;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _hasHeaderRow    = Content?.HasHeaderRow    ?? false;
        _hasHeaderColumn = Content?.HasHeaderColumn ?? false;
        _columnCount     = Content?.ColumnCount     ?? 0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_rowsLoaded && !_loadingRows)
            await LoadRowsAsync();
    }

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
            var newCells = rc.Cells.ToList();
            newCells.Add(string.Empty);
            var updated = BuildRowBlock(_rows[i], new TableRowBlockContent { Cells = newCells });
            try { await Context.BlockProvider.UpdateBlockAsync(updated); _rows[i] = updated; }
            catch { }
        }

        var updatedTable = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = _hasHeaderRow,
            HasHeaderColumn = _hasHeaderColumn,
            ColumnCount     = newCount
        });
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
            var newCells = rc.Cells.ToList();
            if (colIndex < newCells.Count) newCells.RemoveAt(colIndex);
            var updated = BuildRowBlock(_rows[i], new TableRowBlockContent { Cells = newCells });
            try { await Context.BlockProvider.UpdateBlockAsync(updated); _rows[i] = updated; }
            catch { }
        }

        var updatedTable = BuildTableBlock(Block, new TableBlockContent
        {
            HasHeaderRow    = _hasHeaderRow,
            HasHeaderColumn = _hasHeaderColumn,
            ColumnCount     = newCount
        });
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

    private async Task HandleCellsChangedAsync(IPageBlock row, IReadOnlyList<string> cells)
    {
        var updated = BuildRowBlock(row, new TableRowBlockContent { Cells = cells.ToList() });
        var idx = _rows.FindIndex(r => r.Id == row.Id);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            if (idx >= 0) _rows[idx] = updated;
        }
        catch { }
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

    // ── Block builders ────────────────────────────────────────────────────────

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
}
