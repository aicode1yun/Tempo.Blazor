using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Synced;

public partial class TmNotionSyncedBlockRef : ComponentBase, IDisposable
{
    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public ISyncedBlockRefContent? Content { get; set; }

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public bool IsFocused { get; set; }

    [Parameter] public EventCallback OnFocused { get; set; }

    /// <summary>Raised with the new independent block after unsync completes.</summary>
    [Parameter] public EventCallback<IPageBlock> OnUnsync { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private List<IPageBlock>      _children         = [];
    private bool                  _loading;
    private bool                  _childrenLoaded;
    private string?               _originTitle;
    private bool                  _unsyncInProgress;
    private Guid?                 _activeChildId;
    private ISyncedBlockRefContent? _lastContent;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        Context.BlockConverted += OnBlockConverted;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent    = Content;
        _childrenLoaded = false;
        if (Content is not null)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Context.SyncedBlockProvider is null || Content is null) return;
        _loading = true;
        StateHasChanged();
        try
        {
            var syncId   = Content.SyncId.ToString();
            var children = await Context.SyncedBlockProvider.GetSyncedChildBlocksAsync(syncId);
            _children       = [.. children.OrderBy(b => b.Order)];
            _childrenLoaded = true;

            // Load origin page title for the header link
            try
            {
                var originPage = await Context.DataProvider.GetPageAsync(Content.OriginPageId.ToString());
                _originTitle   = originPage?.Title;
            }
            catch
            {
                _originTitle = null;
            }
        }
        catch { }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    // ── Push helper ───────────────────────────────────────────────────────────

    private async Task PushChildrenAsync()
    {
        if (Context.SyncedBlockProvider is null || Content is null) return;
        try
        {
            await Context.SyncedBlockProvider.UpdateSyncedChildBlocksAsync(
                Content.SyncId.ToString(), _children);
        }
        catch { }
    }

    // ── Unsync ────────────────────────────────────────────────────────────────

    private async Task HandleUnsyncAsync()
    {
        if (Context.SyncedBlockProvider is null || _unsyncInProgress) return;
        _unsyncInProgress = true;
        StateHasChanged();
        try
        {
            var newBlock = await Context.SyncedBlockProvider.UnsyncBlockAsync(Block.Id.ToString());
            await OnUnsync.InvokeAsync(newBlock);
        }
        catch { }
        finally
        {
            _unsyncInProgress = false;
            StateHasChanged();
        }
    }

    // ── Navigate to origin ────────────────────────────────────────────────────

    private Task HandleOriginNavigateAsync()
    {
        if (Content is null || Context.NavigateTo is null) return Task.CompletedTask;
        return Context.NavigateTo(Content.OriginPageId.ToString());
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
        RenumberOrder();
        StateHasChanged();
        await PushChildrenAsync();
    }

    // ── Child delete ──────────────────────────────────────────────────────────

    private async Task HandleChildDeletedAsync(string childId)
    {
        var child = _children.FirstOrDefault(b => b.Id.ToString() == childId);
        if (child is null) return;
        try
        {
            await Context.BlockProvider.DeleteBlockAsync(childId);
            _children.Remove(child);
            if (_activeChildId == child.Id) _activeChildId = null;
            StateHasChanged();
            await PushChildrenAsync();
        }
        catch { }
    }

    // ── Child update ──────────────────────────────────────────────────────────

    private async Task HandleChildUpdatedAsync(IPageBlock updated)
    {
        var idx = _children.FindIndex(b => b.Id == updated.Id);
        if (idx >= 0) _children[idx] = updated;
        StateHasChanged();
        await PushChildrenAsync();
    }

    // ── Child duplicate ───────────────────────────────────────────────────────

    private async Task HandleChildDuplicatedAsync(IPageBlock source)
    {
        try
        {
            var duplicated = await Context.BlockProvider.DuplicateBlockAsync(source.Id.ToString());
            var srcIdx     = _children.FindIndex(b => b.Id == source.Id);
            _children.Insert(Math.Clamp(srcIdx + 1, 0, _children.Count), duplicated);
            _activeChildId = duplicated.Id;
            StateHasChanged();
            await PushChildrenAsync();
        }
        catch { }
    }

    // ── Child convert ─────────────────────────────────────────────────────────

    private void OnBlockConverted(IPageBlock converted)
    {
        var idx = _children.FindIndex(b => b.Id == converted.Id);
        if (idx >= 0)
        {
            _children[idx] = converted;
            if (_activeChildId == converted.Id)
                _activeChildId = converted.Id;
            StateHasChanged();
        }
    }

    private async Task HandleChildConvertAsync((string childId, BlockType newType) args)
    {
        var child = _children.FirstOrDefault(b => b.Id.ToString() == args.childId);
        if (child is null) return;
        try
        {
            var converted = await Context.BlockProvider.ConvertBlockTypeAsync(args.childId, args.newType);
            var idx       = _children.FindIndex(b => b.Id == child.Id);
            if (idx >= 0) _children[idx] = converted;
            StateHasChanged();
            await PushChildrenAsync();
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
            var created   = await Context.BlockProvider.CreateBlockAsync(
                Block.PageId.ToString(), newBlock, args.AfterChildId);
            var insertIdx = afterChild is null
                ? _children.Count
                : _children.IndexOf(afterChild) + 1;
            _children.Insert(Math.Clamp(insertIdx, 0, _children.Count), created);
            _activeChildId = created.Id;
            StateHasChanged();
            await PushChildrenAsync();
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
            var created = await Context.BlockProvider.CreateBlockAsync(
                Block.PageId.ToString(), newBlock, null);
            _children.Add(created);
            _activeChildId = created.Id;
            StateHasChanged();
            await PushChildrenAsync();
        }
        catch { }
    }

    public void Dispose()
    {
        Context.BlockConverted -= OnBlockConverted;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RenumberOrder()
    {
        for (var i = 0; i < _children.Count; i++)
            if (_children[i] is PageBlock pb) pb.Order = i;
    }

    private static IBlockContent CreateDefaultContent(BlockType type, string? html = null) => type switch
    {
        BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3
            => new HeadingBlockContent { Html = html ?? string.Empty, Level = type switch { BlockType.Heading1 => 1, BlockType.Heading2 => 2, _ => 3 } },
        BlockType.Quote        => new TextBlockContent    { Html = html ?? string.Empty },
        BlockType.Callout      => new CalloutBlockContent { Html = html ?? string.Empty, IconEmoji = "💡" },
        BlockType.BulletList or
        BlockType.NumberedList => new ListBlockContent    { Html = html ?? string.Empty },
        BlockType.TodoItem     => new TodoBlockContent    { Html = html ?? string.Empty },
        BlockType.Toggle       => new ToggleBlockContent  { Html = html ?? string.Empty },
        BlockType.Code         => new CodeBlockContent(),
        _                      => new TextBlockContent    { Html = html ?? string.Empty }
    };
}
