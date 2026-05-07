using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Page;

/// <summary>
/// Renders a single Notion page: header (via TmNotionPageHeader), block list and comments area.
/// Loads blocks via <see cref="NotionEditorContext.BlockProvider"/> and manages block state.
/// </summary>
public partial class TmNotionPage : ComponentBase, IAsyncDisposable
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>The page to display (required).</summary>
    [Parameter, EditorRequired]
    public INotionPage Page { get; set; } = default!;

    /// <summary>When true all editing interactions are disabled.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>Raised after the page metadata (title, icon, cover) is saved.</summary>
    [Parameter]
    public EventCallback<INotionPage> OnPageUpdated { get; set; }

    /// <summary>Raised when the user clicks a child-page link or mention.</summary>
    [Parameter]
    public EventCallback<string> OnNavigateToPage { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private List<IPageBlock> _blocks          = [];
    private bool             _isLoadingBlocks;
    private string?          _loadBlocksError;
    private Guid?            _activeBlockId;
    private INotionPage?     _lastPage;

    private ElementReference _pageRef;
    private bool             _historyVisible;
    private bool             _collabSubscribed;

    // ── Slash menu state ─────────────────────────────────────────────────────

    private bool   _slashMenuVisible;
    private double _slashMenuTop;
    private double _slashMenuLeft;
    private string _slashBlockId = string.Empty;

    // ── Mention menu state ────────────────────────────────────────────────────

    private bool   _mentionMenuVisible;
    private double _mentionMenuTop;
    private double _mentionMenuLeft;
    private string _mentionBlockId  = string.Empty;
    private bool   _mentionPagesOnly;

    // ── Inline toolbar state ──────────────────────────────────────────────────

    private bool          _toolbarVisible;
    private double        _toolbarTop;
    private double        _toolbarLeft;
    private bool          _toolbarIsBold;
    private bool          _toolbarIsItalic;
    private bool          _toolbarIsUnderline;
    private bool          _toolbarIsStrikethrough;
    private bool          _toolbarIsCode;
    private string        _toolbarCurrentHref  = string.Empty;
    private TextAlignment _toolbarCurrentAlign  = TextAlignment.Left;
    private string        _toolbarBlockId       = string.Empty;
    private DotNetObjectReference<TmNotionPage>? _toolbarDotNetRef;
    private bool          _toolbarWatcherReady;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _pageMods => string.Concat(
        Page.IsFullWidth ? " tm-notion-page--full-width"  : string.Empty,
        Page.IsSmallText ? " tm-notion-page--small-text"  : string.Empty,
        ReadOnly         ? " tm-notion-page--readonly"     : string.Empty
    ).TrimStart();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(Page, _lastPage))
        {
            _lastPage = Page;

            if (!_collabSubscribed && Context.CollaborationSync is { } sync)
            {
                sync.RemoteBlockChanged += OnRemoteBlockChanged;
                _collabSubscribed = true;
            }

            await RefreshAsync();
        }
    }

    private async void OnRemoteBlockChanged(BlockChange _)
    {
        // Remote edit arrived — reload all blocks for the page (last-write-wins)
        await InvokeAsync(RefreshAsync);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ReadOnly)
        {
            _toolbarDotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.initSelectionWatcher", _pageRef, _toolbarDotNetRef);
                _toolbarWatcherReady = true;
            }
            catch { /* SSR / test */ }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Reloads all blocks for the current page.</summary>
    public async Task RefreshAsync()
    {
        if (_isLoadingBlocks) return;

        _isLoadingBlocks = true;
        _loadBlocksError = null;
        StateHasChanged();

        try
        {
            var result = await Context.BlockProvider.GetBlocksAsync(Page.Id.ToString());
            _blocks = [.. result.OrderBy(b => b.Order)];
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
        }
        finally
        {
            _isLoadingBlocks = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Creates a new block of the given type and inserts it after <paramref name="afterBlockId"/>.
    /// Passing <c>null</c> appends the block at the end. <paramref name="initialHtml"/> pre-fills
    /// the new block's HTML content (used when Enter splits a paragraph at the caret position).
    /// </summary>
    public async Task AddBlockAsync(BlockType type, string? afterBlockId = null, string? initialHtml = null)
    {
        if (ReadOnly) return;

        var afterBlock   = afterBlockId is null ? null : _blocks.FirstOrDefault(b => b.Id.ToString() == afterBlockId);
        var insertOrder  = afterBlock is null
            ? (_blocks.Count > 0 ? _blocks.Max(b => b.Order) + 1 : 0)
            : afterBlock.Order + 1;

        var newBlock = new PageBlock
        {
            Id      = Guid.NewGuid(),
            PageId  = Page.Id,
            Type    = type,
            Order   = insertOrder,
            Content = CreateDefaultContent(type, initialHtml)
        };

        try
        {
            var created   = await Context.BlockProvider.CreateBlockAsync(Page.Id.ToString(), newBlock, afterBlockId);
            var insertIdx = afterBlock is null
                ? _blocks.Count
                : _blocks.IndexOf(afterBlock) + 1;

            _blocks.Insert(Math.Clamp(insertIdx, 0, _blocks.Count), created);
            _activeBlockId = created.Id;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
            StateHasChanged();
        }
    }

    /// <summary>Deletes the block with the given ID.</summary>
    public async Task DeleteBlockAsync(string blockId)
    {
        if (ReadOnly) return;

        var block = _blocks.FirstOrDefault(b => b.Id.ToString() == blockId);
        if (block is null) return;

        try
        {
            await Context.BlockProvider.DeleteBlockAsync(blockId);
            _blocks.Remove(block);
            if (_activeBlockId == block.Id) _activeBlockId = null;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Reorders blocks using an optimistic local update, then persists via the provider.
    /// Rolls back on failure.
    /// </summary>
    public async Task ReorderBlocksAsync(int sourceIndex, int targetIndex)
    {
        if (ReadOnly || sourceIndex < 0 || sourceIndex >= _blocks.Count) return;

        var block            = _blocks[sourceIndex];
        var normalizedTarget = Math.Clamp(
            sourceIndex < targetIndex ? targetIndex - 1 : targetIndex,
            0, _blocks.Count - 1);

        _blocks.RemoveAt(sourceIndex);
        _blocks.Insert(normalizedTarget, block);
        RenumberLocalOrder();
        StateHasChanged();

        try
        {
            await Context.BlockProvider.ReorderBlocksAsync(
                Page.Id.ToString(),
                _blocks.Select(b => b.Id.ToString()));
        }
        catch
        {
            await RefreshAsync();
        }
    }

    /// <summary>Sets the currently focused block. Called by child block components.</summary>
    public void SetActiveBlock(Guid? blockId)
    {
        if (_activeBlockId == blockId) return;
        _activeBlockId = blockId;
        StateHasChanged();
    }

    /// <summary>Returns the current ordered list of blocks (read-only view).</summary>
    public IReadOnlyList<IPageBlock> Blocks => _blocks;

    // ── Page settings handlers ────────────────────────────────────────────────

    private async Task OnPageSettingsUpdated(INotionPage updated)
    {
        await OnPageUpdated.InvokeAsync(updated);
        StateHasChanged();
    }

    private async Task HandlePageDeletedAsync()
    {
        await OnNavigateToPage.InvokeAsync(string.Empty);
    }

    private Task HandlePageHistoryRequestedAsync()
    {
        _historyVisible = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleNavigateToImportedPageAsync(string pageId)
    {
        await OnNavigateToPage.InvokeAsync(pageId);
    }

    private async Task HandleHistoryRestoredAsync(string pageId)
    {
        _historyVisible = false;
        await RefreshAsync();
    }

    // ── Title enter handler ───────────────────────────────────────────────────

    private async Task HandleTitleEnterPressedAsync() =>
        await AddBlockAsync(BlockType.Paragraph);

    // ── Block list event handlers ─────────────────────────────────────────────

    private async Task HandleBlockReorderAsync((int source, int target) args) =>
        await ReorderBlocksAsync(args.source, args.target);

    private Task HandleBlockFocusedAsync(string blockId)
    {
        if (Guid.TryParse(blockId, out var id)) SetActiveBlock(id);
        return Task.CompletedTask;
    }

    private async Task HandleBlockDeletedAsync(string blockId) =>
        await DeleteBlockAsync(blockId);

    private Task HandleBlockUpdatedAsync(IPageBlock updated)
    {
        var idx = _blocks.FindIndex(b => b.Id == updated.Id);
        if (idx < 0) return Task.CompletedTask;
        _blocks[idx] = updated;
        _blocks = [.._blocks]; // new reference — CascadingValue consumers (ToC) detect the change
        return Task.CompletedTask;
    }

    private async Task HandleConvertBlockAsync((string blockId, BlockType newType) args)
    {
        if (ReadOnly) return;

        try
        {
            var converted = await Context.BlockProvider.ConvertBlockTypeAsync(args.blockId, args.newType);
            var idx = _blocks.FindIndex(b => b.Id == converted.Id);
            if (idx >= 0) _blocks[idx] = converted;
            Context.RaiseBlockConverted(converted);
            _activeBlockId = converted.Id;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
            StateHasChanged();
        }
    }

    private async Task HandleAddBlockAfterAsync((string AfterBlockId, BlockType Type, string? InitialHtml) args) =>
        await AddBlockAsync(args.Type, args.AfterBlockId, args.InitialHtml);

    private async Task HandleInsertTemplateBlocksAfterAsync((string AfterBlockId, IReadOnlyList<IPageBlock> Blocks) args)
    {
        if (ReadOnly || args.Blocks.Count == 0) return;

        var afterBlock = _blocks.FirstOrDefault(b => b.Id.ToString() == args.AfterBlockId);
        var baseOrder  = afterBlock?.Order ?? (_blocks.Count > 0 ? _blocks.Max(b => b.Order) : 0);

        var newBlocks = args.Blocks.Select((src, i) => (IPageBlock)new PageBlock
        {
            Id           = Guid.NewGuid(),
            PageId       = Page.Id,
            Type         = src.Type,
            Order        = baseOrder + i + 1,
            Content      = src.Content,
            CreatedAt    = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        }).ToList();

        try
        {
            var created   = await Context.BlockProvider.CreateBlocksAsync(Page.Id.ToString(), newBlocks, args.AfterBlockId);
            var insertIdx = afterBlock is null ? _blocks.Count : _blocks.IndexOf(afterBlock) + 1;
            foreach (var b in created.OrderBy(b => b.Order))
                _blocks.Insert(Math.Clamp(insertIdx++, 0, _blocks.Count), b);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
            StateHasChanged();
        }
    }

    private async Task HandleAddBlockAtEndAsync() =>
        await AddBlockAsync(BlockType.Paragraph);

    private async Task HandleBlockDuplicatedAsync(IPageBlock source) =>
        await DuplicateBlockAsync(source);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates an identical copy of <paramref name="source"/> and inserts it after it.</summary>
    public async Task DuplicateBlockAsync(IPageBlock source)
    {
        if (ReadOnly) return;

        var duplicate = new PageBlock
        {
            Id            = Guid.NewGuid(),
            PageId        = source.PageId,
            ParentBlockId = source.ParentBlockId,
            Type          = source.Type,
            Order         = source.Order + 1,
            Content       = source.Content,
            CreatedAt     = DateTime.UtcNow,
            LastEditedAt  = DateTime.UtcNow
        };

        try
        {
            var created  = await Context.BlockProvider.CreateBlockAsync(
                source.PageId.ToString(), duplicate, source.Id.ToString());
            var srcIdx   = _blocks.FindIndex(b => b.Id == source.Id);
            var insertAt = srcIdx >= 0 ? srcIdx + 1 : _blocks.Count;
            _blocks.Insert(Math.Clamp(insertAt, 0, _blocks.Count), created);
            _activeBlockId = created.Id;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _loadBlocksError = ex.Message;
            StateHasChanged();
        }
    }

    // ── Click / keyboard on content area ─────────────────────────────────────

    private async Task OnContentAreaClickAsync()
    {
        if (!ReadOnly && _blocks.Count == 0)
            await AddBlockAsync(BlockType.Paragraph);
    }

    private async Task OnEmptyHintClickAsync()
    {
        if (!ReadOnly) await AddBlockAsync(BlockType.Paragraph);
    }

    private async Task OnEmptyHintKeyDownAsync(KeyboardEventArgs e)
    {
        if (!ReadOnly && (e.Key == "Enter" || e.Key == " "))
            await AddBlockAsync(BlockType.Paragraph);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RenumberLocalOrder()
    {
        for (var i = 0; i < _blocks.Count; i++)
        {
            if (_blocks[i] is PageBlock pb) pb.Order = i;
        }
    }

    private static IBlockContent CreateDefaultContent(BlockType type, string? initialHtml = null) => type switch
    {
        BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3
            => new HeadingBlockContent
            {
                Html  = initialHtml,
                Level = type switch { BlockType.Heading1 => 1, BlockType.Heading2 => 2, _ => 3 }
            },
        BlockType.Quote        => new TextBlockContent  { Html = initialHtml },
        BlockType.Callout      => new CalloutBlockContent { IconEmoji = "💡", Html = initialHtml },
        BlockType.Code         => new CodeBlockContent(),
        BlockType.Equation     => new EquationBlockContent { Expression = string.Empty },
        BlockType.Divider      => new DividerBlockContent(),
        BlockType.BulletList   => new ListBlockContent  { Html = initialHtml },
        BlockType.NumberedList => new ListBlockContent  { Html = initialHtml },
        BlockType.TodoItem     => new TodoBlockContent  { Html = initialHtml },
        BlockType.Toggle       => new ToggleBlockContent { Html = initialHtml },
        _                      => new TextBlockContent  { Html = initialHtml }
    };

    // ── Slash menu handlers ───────────────────────────────────────────────────

    private Task HandleSlashMenuOpenedAsync((string BlockId, double Top, double Left) args)
    {
        _slashBlockId   = args.BlockId;
        _slashMenuTop   = args.Top;
        _slashMenuLeft  = args.Left;
        _slashMenuVisible = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleSlashItemSelectedAsync(BlockType selectedType)
    {
        _slashMenuVisible = false;

        if (!string.IsNullOrEmpty(_slashBlockId))
            await HandleConvertBlockAsync((_slashBlockId, selectedType));

        _slashBlockId = string.Empty;
        StateHasChanged();
    }

    private Task HandleSlashMenuClosedAsync()
    {
        _slashMenuVisible = false;
        _slashBlockId     = string.Empty;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Mention menu handlers ─────────────────────────────────────────────────

    private Task HandleMentionMenuOpenedAsync((string BlockId, double Top, double Left) args)
    {
        _mentionBlockId     = args.BlockId;
        _mentionMenuTop     = args.Top;
        _mentionMenuLeft    = args.Left;
        _mentionMenuVisible = true;
        _mentionPagesOnly   = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task HandlePageLinkMenuOpenedAsync((string BlockId, double Top, double Left) args)
    {
        _mentionBlockId     = args.BlockId;
        _mentionMenuTop     = args.Top;
        _mentionMenuLeft    = args.Left;
        _mentionMenuVisible = true;
        _mentionPagesOnly   = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleMentionItemSelectedAsync((string Type, string Id, string Display) args)
    {
        _mentionMenuVisible = false;
        _mentionBlockId     = string.Empty;
        StateHasChanged();

        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.insertMentionChip", args.Type, args.Id, args.Display);
        }
        catch { }
    }

    private Task HandleMentionMenuClosedAsync()
    {
        _mentionMenuVisible = false;
        _mentionBlockId     = string.Empty;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Inline toolbar JS callbacks ───────────────────────────────────────────

    [JSInvokable]
    public Task OnToolbarSelectionChanged(
        double top, double left,
        bool isBold, bool isItalic, bool isUnderline, bool isStrikethrough, bool isCode,
        string currentHref, string blockId)
    {
        _toolbarVisible        = true;
        _toolbarTop            = top;
        _toolbarLeft           = left;
        _toolbarIsBold         = isBold;
        _toolbarIsItalic       = isItalic;
        _toolbarIsUnderline    = isUnderline;
        _toolbarIsStrikethrough = isStrikethrough;
        _toolbarIsCode         = isCode;
        _toolbarCurrentHref    = currentHref;
        _toolbarBlockId        = blockId;

        var block = _blocks.FirstOrDefault(b => b.Id.ToString() == blockId);
        _toolbarCurrentAlign = block?.Content is ITextBlockContent tc ? tc.Alignment : TextAlignment.Left;

        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnToolbarSelectionCleared()
    {
        if (!_toolbarVisible) return Task.CompletedTask;
        _toolbarVisible = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── Inline toolbar event handlers ─────────────────────────────────────────

    private async Task HandleToolbarTurnIntoAsync(BlockType newType)
    {
        if (!string.IsNullOrEmpty(_toolbarBlockId))
            await HandleConvertBlockAsync((_toolbarBlockId, newType));
    }

    private async Task HandleToolbarAlignAsync(TextAlignment alignment)
    {
        _toolbarCurrentAlign = alignment;
        var block = _blocks.FirstOrDefault(b => b.Id.ToString() == _toolbarBlockId);
        if (block is null) return;

        var applied = block.Content switch
        {
            TextBlockContent    tb => (tb.Alignment = alignment) == alignment,
            HeadingBlockContent hb => (hb.Alignment = alignment) == alignment,
            ListBlockContent    lb => (lb.Alignment = alignment) == alignment,
            TodoBlockContent    td => (td.Alignment = alignment) == alignment,
            ToggleBlockContent  tg => (tg.Alignment = alignment) == alignment,
            CalloutBlockContent cb => (cb.Alignment = alignment) == alignment,
            _                      => false
        };

        if (applied)
        {
            try { await Context.BlockProvider.UpdateBlockAsync(block); }
            catch { /* best-effort */ }
            StateHasChanged();
        }
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_collabSubscribed && Context.CollaborationSync is { } sync)
            sync.RemoteBlockChanged -= OnRemoteBlockChanged;

        if (_toolbarWatcherReady)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroySelectionWatcher", _pageRef); }
            catch { }
        }
        _toolbarDotNetRef?.Dispose();
    }
}
