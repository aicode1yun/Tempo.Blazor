using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Page;

/// <summary>Simple DOMRect wrapper for JS interop.</summary>
internal sealed class DomRect
{
    public double Top    { get; set; }
    public double Left   { get; set; }
    public double Width  { get; set; }
    public double Height { get; set; }
}

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

    // ── Block comment panel state ─────────────────────────────────────────────

    private bool   _blockCommentVisible;
    private string _blockCommentBlockId = string.Empty;
    private double _blockCommentTop;
    private double _blockCommentLeft;
    private bool   _blockCommentStartInNewThreadMode;
    /// <summary>Per-block comment summary for margin-thread indicators and hover tooltip.</summary>
    public sealed record BlockCommentInfo(
        int Unresolved,
        int ResolvedUnread,
        bool HasUnreadActivity,
        string? LastAuthorName,
        string? LastAuthorAvatar,
        string? LastEntryText,
        DateTime? LastEntryTime,
        int ThreadCount);

    private readonly Dictionary<string, BlockCommentInfo> _blockCommentCounts = new();

    // ── Text comment panel state ──────────────────────────────────────────────

    private bool   _textCommentVisible;
    private string _textCommentId       = string.Empty;
    private string _textCommentBlockId  = string.Empty;
    private double _textCommentTop;
    private double _textCommentLeft;

    // ── Page comment panel state ──────────────────────────────────────────────

    private bool _pageCommentExpanded;

    // ── Page-level unresolved comment count (header badge) ────────────────────

    private int _pageUnresolvedCommentCount;

    // ── Slash menu state ─────────────────────────────────────────────────────

    private bool   _slashMenuVisible;
    private double _slashMenuTop;
    private double _slashMenuLeft;
    private string _slashBlockId = string.Empty;

    // ── Token dropdown state ──────────────────────────────────────────────────

    private bool   _tokenDropdownVisible;
    private double _tokenDropdownTop;
    private double _tokenDropdownLeft;
    private string _tokenBlockId = string.Empty;

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
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.registerPageDotNetRef", _toolbarDotNetRef);
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
            await LoadPageUnresolvedCommentCountAsync();
            StateHasChanged();
        }
    }

    /// <summary>Loads the total unresolved comment count for the current page (header badge).</summary>
    private async Task LoadPageUnresolvedCommentCountAsync()
    {
        if (Context.CommentProvider is null) return;
        try
        {
            _pageUnresolvedCommentCount = await Context.CommentProvider.GetUnresolvedCommentsCountAsync(Page.Id.ToString());
        }
        catch
        {
            _pageUnresolvedCommentCount = 0;
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
        StateHasChanged();
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

    // ── Comment handlers ──────────────────────────────────────────────────────

    private async Task HandleBlockCommentAsync(string blockId)
    {
        _blockCommentBlockId = blockId;
        _blockCommentTop     = 150;
        _blockCommentLeft    = 300;
        _blockCommentStartInNewThreadMode = false;
        _blockCommentVisible = true;
        StateHasChanged();

        // Mark threads as read for current user
        if (Context.CommentProvider is not null)
        {
            try
            {
                var comments = await Context.CommentProvider.GetBlockCommentsAsync(blockId);
                foreach (var c in comments)
                    await Context.CommentProvider.MarkThreadAsReadAsync(c.Id.ToString(), "demo");
                await HandleBlockCommentCountChangedAsync(blockId);
            }
            catch { }
        }

        // Try to position near the block
        try
        {
            var rect = await JS.InvokeAsync<DomRect>("tmNotionEditor.getBlockBoundingRect", blockId);
            if (rect is not null)
            {
                _blockCommentTop  = rect.Top;
                _blockCommentLeft = rect.Left + rect.Width + 8;
                StateHasChanged();
            }
        }
        catch { }

        // Pre-load comment counts for margin thread indicator (resolved = resolved-but-unread)
        if (Context.CommentProvider is not null)
        {
            try
            {
                var comments = await Context.CommentProvider.GetBlockCommentsAsync(blockId);
                _blockCommentCounts[blockId] = ComputeBlockCommentInfo(comments);
                StateHasChanged();
            }
            catch { }
        }
    }

    private Task HandleBlockCommentClosedAsync()
    {
        _blockCommentVisible = false;
        _blockCommentBlockId = string.Empty;
        _blockCommentStartInNewThreadMode = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleBlockCommentCountChangedAsync(string blockId)
    {
        if (Context.CommentProvider is null || string.IsNullOrEmpty(blockId)) return;
        try
        {
            var comments = await Context.CommentProvider.GetBlockCommentsAsync(blockId);
            var info = ComputeBlockCommentInfo(comments);
            if (info.Unresolved > 0 || info.ResolvedUnread > 0)
                _blockCommentCounts[blockId] = info;
            else
                _blockCommentCounts.Remove(blockId);
            await LoadPageUnresolvedCommentCountAsync();
            StateHasChanged();
        }
        catch { }
    }

    private static BlockCommentInfo ComputeBlockCommentInfo(IEnumerable<IBlockComment> comments)
    {
        var list = comments.ToList();
        var unresolved   = list.Count(c => !c.IsResolved);
        var resolvedUnread = list.Count(c => c.IsResolved && !c.ReadByUserIds.Contains("demo"));

        // Find the latest entry across all threads for tooltip data
        INotionCommentEntry? latest = null;
        foreach (var c in list)
        {
            foreach (var e in c.Thread)
            {
                if (latest is null || e.CreatedAt > latest.CreatedAt)
                    latest = e;
            }
        }

        var text = latest?.HtmlContent;
        if (!string.IsNullOrEmpty(text))
        {
            // Strip HTML tags for tooltip preview
            text = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
            if (text.Length > 120)
                text = text[..120] + "…";
        }

        var hasUnread = list.Any(c => c.LastActivityAt.HasValue && !c.ReadByUserIds.Contains("demo"));

        return new BlockCommentInfo(
            unresolved,
            resolvedUnread,
            hasUnread,
            latest?.AuthorDisplayName,
            latest?.AuthorAvatarUrl,
            text,
            latest?.CreatedAt,
            list.Count);
    }

    private async Task HandleBlockNewThreadAsync(string blockId)
    {
        _blockCommentBlockId = blockId;
        _blockCommentTop     = 150;
        _blockCommentLeft    = 300;
        _blockCommentStartInNewThreadMode = true;
        _blockCommentVisible = true;
        StateHasChanged();

        // Mark existing threads as read for current user
        if (Context.CommentProvider is not null)
        {
            try
            {
                var comments = await Context.CommentProvider.GetBlockCommentsAsync(blockId);
                foreach (var c in comments)
                    await Context.CommentProvider.MarkThreadAsReadAsync(c.Id.ToString(), "demo");
                await HandleBlockCommentCountChangedAsync(blockId);
            }
            catch { }
        }

        // Try to position near the block
        try
        {
            var rect = await JS.InvokeAsync<DomRect>("tmNotionEditor.getBlockBoundingRect", blockId);
            if (rect is not null)
            {
                _blockCommentTop  = rect.Top;
                _blockCommentLeft = rect.Left + rect.Width + 8;
                StateHasChanged();
            }
        }
        catch { }

        // Pre-load comment counts
        if (Context.CommentProvider is not null)
        {
            try
            {
                var comments = await Context.CommentProvider.GetBlockCommentsAsync(blockId);
                _blockCommentCounts[blockId] = ComputeBlockCommentInfo(comments);
                StateHasChanged();
            }
            catch { }
        }
    }

    private Task HandleTextCommentAsync(string commentId)
    {
        _textCommentId       = commentId;
        _textCommentBlockId  = _toolbarBlockId;
        _textCommentTop      = _toolbarTop;
        _textCommentLeft     = _toolbarLeft + 40;
        _textCommentVisible  = true;
        _toolbarVisible      = false; // hide toolbar when comment panel opens
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task HandleTextCommentClosedAsync()
    {
        _textCommentVisible = false;
        _textCommentId      = string.Empty;
        _textCommentBlockId = string.Empty;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleTextCommentResolvedAsync()
    {
        _textCommentVisible = false;
        _textCommentId      = string.Empty;
        _textCommentBlockId = string.Empty;
        await LoadPageUnresolvedCommentCountAsync();
        StateHasChanged();
    }

    private Task HandlePageCommentExpandedChangedAsync(bool expanded)
    {
        _pageCommentExpanded = expanded;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Scrolls the page to the first block with an unresolved comment (header badge click).</summary>
    private async Task HandleCommentBadgeClickedAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.scrollToFirstUnresolvedComment");
        }
        catch { /* SSR / test */ }
    }

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
        BlockType.Toggle       => new ToggleBlockContent   { Html = initialHtml },
        BlockType.Table        => new TableBlockContent(),
        BlockType.ColumnList   => new ColumnListBlockContent(),
        BlockType.Breadcrumb   => new BreadcrumbBlockContent(),
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

    // ── Token dropdown handlers ────────────────────────────────────────────────

    private Task HandleTokenMenuOpenedAsync((string BlockId, double Top, double Left) args)
    {
        _tokenBlockId          = args.BlockId;
        _tokenDropdownTop      = args.Top;
        _tokenDropdownLeft     = args.Left;
        _tokenDropdownVisible  = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleTokenItemSelectedAsync((string Key, string DisplayName, string? ColorClass) args)
    {
        _tokenDropdownVisible = false;
        _tokenBlockId         = string.Empty;
        StateHasChanged();

        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.insertNotionToken", args.Key, args.DisplayName, args.ColorClass);
        }
        catch { }
    }

    private async Task HandleTokenDropdownClosedAsync()
    {
        _tokenDropdownVisible = false;
        _tokenBlockId         = string.Empty;
        StateHasChanged();

        try { await JS.InvokeVoidAsync("tmNotionEditor.cancelTokenTrigger"); } catch { }
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

    [JSInvokable]
    public Task OnTextCommentCreated(string blockId, string commentId, string highlightedText, int startOffset, int endOffset, double top, double left)
    {
        _textCommentId       = commentId;
        _textCommentBlockId  = blockId;
        _textCommentTop      = top;
        _textCommentLeft     = left + 40;
        _textCommentVisible  = true;
        _toolbarVisible      = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnTextCommentMarkClicked(string commentId, string blockId, double top, double left)
    {
        _textCommentId      = commentId;
        _textCommentBlockId = blockId;
        _textCommentTop     = top;
        _textCommentLeft    = left + 40;
        _textCommentVisible = true;
        _toolbarVisible     = false;
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
