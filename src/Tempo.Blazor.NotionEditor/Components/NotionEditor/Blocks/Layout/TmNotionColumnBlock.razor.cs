using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Layout;

public partial class TmNotionColumnBlock : ComponentBase, IDisposable
{
    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public int    ColumnIndex  { get; set; }
    [Parameter] public double WidthPercent { get; set; } = 50;
    [Parameter] public bool   ReadOnly     { get; set; }

    [Parameter] public EventCallback OnFocused { get; set; }
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnSlashMenu { get; set; }
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnMentionMenu { get; set; }
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnPageLinkMenu { get; set; }
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnTokenMenu { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private List<IPageBlock> _children        = [];
    private bool             _loadingChildren;
    private bool             _childrenLoaded;
    private Guid?            _activeChildId;
    private IPageBlock?      _lastBlock;
    private ElementReference _elementRef;
    private double           _lastAppliedWidthPercent = -1;

    // ── Computed ─────────────────────────────────────────────────────────────

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        Context.BlockConverted += OnBlockConverted;
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Block, _lastBlock)) return;
        _lastBlock               = Block;
        _childrenLoaded          = false;
        _lastAppliedWidthPercent = -1; // force re-apply width to new element
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_childrenLoaded && !_loadingChildren)
            await LoadChildrenAsync();

        if (Math.Abs(WidthPercent - _lastAppliedWidthPercent) > 0.01)
        {
            _lastAppliedWidthPercent = WidthPercent;
            if (_elementRef.Context is not null)
            {
                try
                {
                    await JS.InvokeVoidAsync("tmNotionEditor.setColumnWidth", _elementRef, WidthPercent);
                }
                catch { }
            }
        }
    }

    // ── Children loading ──────────────────────────────────────────────────────

    private async Task LoadChildrenAsync()
    {
        _loadingChildren = true;
        StateHasChanged();
        try
        {
            var result = await Context.BlockService.GetChildBlocksAsync(Block.Id.ToString());
            _children       = [.. result.OrderBy(b => b.Order)];
            _childrenLoaded = true;
        }
        catch { }
        finally
        {
            _loadingChildren = false;
            StateHasChanged();
        }
    }

    // ── Child focus ───────────────────────────────────────────────────────────

    private Task HandleChildFocusedAsync(string childId)
    {
        if (Guid.TryParse(childId, out var id)) _activeChildId = id;
        return OnFocused.HasDelegate ? OnFocused.InvokeAsync() : Task.CompletedTask;
    }

    // ── Child reorder ─────────────────────────────────────────────────────────

    private async Task HandleChildReorderAsync((int source, int target) args)
    {
        if (args.source < 0 || args.source >= _children.Count) return;

        var block  = _children[args.source];
        var target = Math.Clamp(
            args.source < args.target ? args.target - 1 : args.target,
            0, _children.Count - 1);

        _children.RemoveAt(args.source);
        _children.Insert(target, block);
        RenumberChildOrder();
        StateHasChanged();

        try
        {
            await Context.BlockService.ReorderBlocksAsync(
                Block.PageId.ToString(),
                _children.Select(b => b.Id.ToString()));
        }
        catch { await LoadChildrenAsync(); }
    }

    private async Task HandleExternalChildDroppedAsync(MoveNotionBlockRequest request)
    {
        try
        {
            await Context.BlockService.MoveBlockAsync(request);
            _childrenLoaded = false;
            await LoadChildrenAsync();
        }
        catch
        {
            _childrenLoaded = false;
            await LoadChildrenAsync();
        }
    }

    private Task HandleExternalChildRemovedAsync(string childId)
    {
        var child = _children.FirstOrDefault(b => b.Id.ToString() == childId);
        if (child is not null)
        {
            _children.Remove(child);
            if (_activeChildId == child.Id) _activeChildId = null;
            StateHasChanged();
        }

        return Task.CompletedTask;
    }

    // ── Child delete ──────────────────────────────────────────────────────────

    private async Task HandleChildDeletedAsync(string childId)
    {
        var child = _children.FirstOrDefault(b => b.Id.ToString() == childId);
        if (child is null) return;
        try
        {
            await Context.BlockService.DeleteBlockAsync(childId);
            _children.Remove(child);
            if (_activeChildId == child.Id) _activeChildId = null;
            StateHasChanged();
        }
        catch { }
    }

    // ── Child update ──────────────────────────────────────────────────────────

    private Task HandleChildUpdatedAsync(IPageBlock updated)
    {
        var idx = _children.FindIndex(b => b.Id == updated.Id);
        if (idx >= 0) _children[idx] = updated;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Child duplicate ───────────────────────────────────────────────────────

    private async Task HandleChildDuplicatedAsync(IPageBlock source)
    {
        try
        {
            var duplicated = await Context.BlockService.DuplicateBlockAsync(source.Id.ToString());
            var srcIdx     = _children.FindIndex(b => b.Id == source.Id);
            _children.Insert(Math.Clamp(srcIdx + 1, 0, _children.Count), duplicated);
            _activeChildId = duplicated.Id;
            StateHasChanged();
        }
        catch { }
    }

    // ── Child convert ─────────────────────────────────────────────────────────

    private async Task HandleChildConvertAsync((string childId, BlockType newType) args)
    {
        var child = _children.FirstOrDefault(b => b.Id.ToString() == args.childId);
        if (child is null) return;
        try
        {
            var converted = await Context.BlockService.ConvertBlockTypeAsync(args.childId, args.newType);
            var idx       = _children.FindIndex(b => b.Id == child.Id);
            if (idx >= 0) _children[idx] = converted;
            StateHasChanged();
        }
        catch { }
    }

    // ── Child add after ───────────────────────────────────────────────────────

    private async Task HandleChildAddAfterAsync(
        (string AfterChildId, BlockType Type, string? InitialHtml) args)
    {
        var afterChild  = _children.FirstOrDefault(b => b.Id.ToString() == args.AfterChildId);
        var insertOrder = afterChild is null
            ? (_children.Count > 0 ? _children.Max(b => b.Order) + 1 : 0)
            : afterChild.Order + 1;

        var newBlock = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = Block.PageId,
            ParentBlockId = Block.Id,
            Type          = args.Type,
            Order         = insertOrder,
            Content       = CreateDefaultContent(args.Type, args.InitialHtml)
        };

        try
        {
            var created   = await Context.BlockService.CreateBlockAsync(
                Block.PageId.ToString(), newBlock, args.AfterChildId);
            var insertIdx = afterChild is null
                ? _children.Count
                : _children.IndexOf(afterChild) + 1;
            _children.Insert(Math.Clamp(insertIdx, 0, _children.Count), created);
            _activeChildId = created.Id;
            StateHasChanged();
        }
        catch { }
    }

    // ── Child add at end ──────────────────────────────────────────────────────

    private async Task HandleChildAddAtEndAsync()
    {
        var newBlock = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = Block.PageId,
            ParentBlockId = Block.Id,
            Type          = BlockType.Paragraph,
            Order         = _children.Count > 0 ? _children.Max(b => b.Order) + 1 : 0,
            Content       = new TextBlockContent()
        };
        try
        {
            var created = await Context.BlockService.CreateBlockAsync(
                Block.PageId.ToString(), newBlock, null);
            _children.Add(created);
            _activeChildId = created.Id;
            StateHasChanged();
        }
        catch { }
    }

    private void OnBlockConverted(IPageBlock converted)
    {
        var idx = _children.FindIndex(b => b.Id == converted.Id);
        if (idx >= 0)
        {
            _children[idx] = converted;
            if (_activeChildId == converted.Id)
                _activeChildId = converted.Id; // keep focus reference fresh
            StateHasChanged();
        }
    }

    private Task HandleChildSlashAsync((string BlockId, double Top, double Left) args) =>
        OnSlashMenu.InvokeAsync(args);
    private Task HandleChildMentionAsync((string BlockId, double Top, double Left) args) =>
        OnMentionMenu.InvokeAsync(args);
    private Task HandleChildPageLinkAsync((string BlockId, double Top, double Left) args) =>
        OnPageLinkMenu.InvokeAsync(args);
    private Task HandleChildTokenAsync((string BlockId, double Top, double Left) args) =>
        OnTokenMenu.InvokeAsync(args);

    public void Dispose()
    {
        Context.BlockConverted -= OnBlockConverted;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RenumberChildOrder()
    {
        for (var i = 0; i < _children.Count; i++)
        {
            if (_children[i] is PageBlock pb) pb.Order = i;
        }
    }

    private static IBlockContent CreateDefaultContent(BlockType type, string? initialHtml = null) =>
        type switch
        {
            BlockType.Heading1     => new HeadingBlockContent { Html = initialHtml ?? string.Empty, Level = 1 },
            BlockType.Heading2     => new HeadingBlockContent { Html = initialHtml ?? string.Empty, Level = 2 },
            BlockType.Heading3     => new HeadingBlockContent { Html = initialHtml ?? string.Empty, Level = 3 },
            BlockType.Quote        => new TextBlockContent    { Html = initialHtml ?? string.Empty },
            BlockType.Callout      => new CalloutBlockContent { Html = initialHtml ?? string.Empty },
            BlockType.BulletList or
            BlockType.NumberedList => new ListBlockContent    { Html = initialHtml ?? string.Empty },
            BlockType.TodoItem     => new TodoBlockContent    { Html = initialHtml ?? string.Empty },
            BlockType.Toggle       => new ToggleBlockContent  { Html = initialHtml ?? string.Empty },
            BlockType.Code         => new CodeBlockContent(),
            _                      => new TextBlockContent    { Html = initialHtml ?? string.Empty }
        };
}
