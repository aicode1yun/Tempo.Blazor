using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Layout;

public partial class TmNotionColumnListBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public IColumnListBlockContent? Content  { get; set; }
    [Parameter] public bool                     ReadOnly { get; set; }

    [Parameter] public EventCallback<IPageBlock> OnUpdated { get; set; }
    [Parameter] public EventCallback             OnFocused { get; set; }

    // ── Constants ────────────────────────────────────────────────────────────

    private const int MaxColumns = 5;

    // ── State ────────────────────────────────────────────────────────────────

    private List<IPageBlock>                            _columns        = [];
    private bool                                        _loadingColumns;
    private bool                                        _columnsLoaded;
    private ElementReference                            _containerRef;
    private DotNetObjectReference<TmNotionColumnListBlock>? _dotNetRef;
    private bool                                        _resizeInitialized;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_columnsLoaded && !_loadingColumns)
            await LoadColumnsAsync();

        if (_columnsLoaded && !ReadOnly && !_resizeInitialized && _columns.Count > 1)
        {
            _resizeInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initColumnResize", _containerRef, _dotNetRef); }
            catch { }
        }
    }

    // ── Column loading ────────────────────────────────────────────────────────

    private async Task LoadColumnsAsync()
    {
        _loadingColumns = true;
        StateHasChanged();
        try
        {
            var result = await Context.BlockProvider.GetChildBlocksAsync(Block.Id.ToString());
            _columns = result
                .Where(b => b.Type == BlockType.Column)
                .OrderBy(b => (b.Content as IColumnBlockContent)?.ColumnIndex ?? b.Order)
                .ToList();
            _columnsLoaded     = true;
            _resizeInitialized = false;
        }
        catch { }
        finally
        {
            _loadingColumns = false;
            StateHasChanged();
        }
    }

    // ── Width helpers ─────────────────────────────────────────────────────────

    private double GetColumnWidthPercent(int idx)
    {
        if (idx >= _columns.Count) return 100.0 / Math.Max(1, _columns.Count);
        var pct = (_columns[idx].Content as IColumnBlockContent)?.WidthPercent ?? 0;
        return pct > 0 ? pct : 100.0 / Math.Max(1, _columns.Count);
    }

    // ── JS callback — column drag end ─────────────────────────────────────────

    [JSInvokable]
    public async Task OnColumnResized(double[] widths)
    {
        for (var i = 0; i < Math.Min(widths.Length, _columns.Count); i++)
        {
            var col     = _columns[i];
            var updated = BuildColumnBlock(col, new ColumnBlockContent
            {
                ColumnIndex  = i,
                WidthPercent = Math.Round(widths[i], 2)
            });
            try { await Context.BlockProvider.UpdateBlockAsync(updated); }
            catch { }
            _columns[i] = updated;
        }
        StateHasChanged();
    }

    // ── Add column ────────────────────────────────────────────────────────────

    private async Task AddColumnAsync()
    {
        var newIndex  = _columns.Count;
        var totalCols = newIndex + 1;
        var equalPct  = Math.Round(100.0 / totalCols, 2);

        // Redistribute existing column widths
        for (var i = 0; i < _columns.Count; i++)
        {
            var upd = BuildColumnBlock(_columns[i], new ColumnBlockContent
            {
                ColumnIndex  = i,
                WidthPercent = equalPct
            });
            try { await Context.BlockProvider.UpdateBlockAsync(upd); _columns[i] = upd; }
            catch { }
        }

        // Create new Column block
        var newColBlock = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = Block.PageId,
            ParentBlockId = Block.Id,
            Type          = BlockType.Column,
            Order         = newIndex,
            Content       = new ColumnBlockContent { ColumnIndex = newIndex, WidthPercent = equalPct }
        };

        try
        {
            var created = await Context.BlockProvider.CreateBlockAsync(
                Block.PageId.ToString(),
                newColBlock,
                _columns.LastOrDefault()?.Id.ToString());
            _columns.Add(created);

            // Update ColumnList block metadata
            var updatedList = BuildColumnListBlock(Block, new ColumnListBlockContent { ColumnCount = totalCols });
            await Context.BlockProvider.UpdateBlockAsync(updatedList);
            await OnUpdated.InvokeAsync(updatedList);

            _resizeInitialized = false;
            StateHasChanged();
        }
        catch { }
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task OnFocusedAsync() => await OnFocused.InvokeAsync();

    // ── Block builders ────────────────────────────────────────────────────────

    private static PageBlock BuildColumnBlock(IPageBlock src, ColumnBlockContent content) => new()
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

    private static PageBlock BuildColumnListBlock(IPageBlock src, ColumnListBlockContent content) => new()
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

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyColumnResize", _containerRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
