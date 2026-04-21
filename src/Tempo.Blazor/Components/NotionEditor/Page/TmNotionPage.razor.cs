using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
            await RefreshAsync();
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
        if (idx >= 0) _blocks[idx] = updated;
        return Task.CompletedTask;
    }

    private async Task HandleConvertBlockAsync((string blockId, BlockType newType) args)
    {
        var block = _blocks.FirstOrDefault(b => b.Id.ToString() == args.blockId);
        if (block is null || ReadOnly) return;

        var converted = new PageBlock
        {
            Id            = block.Id,
            PageId        = block.PageId,
            ParentBlockId = block.ParentBlockId,
            Type          = args.newType,
            Order         = block.Order,
            Content       = CreateDefaultContent(args.newType),
            CreatedAt     = block.CreatedAt,
            LastEditedAt  = DateTime.UtcNow
        };

        try
        {
            await Context.BlockProvider.UpdateBlockAsync(converted);
            var idx = _blocks.FindIndex(b => b.Id == block.Id);
            if (idx >= 0) _blocks[idx] = converted;
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

    // ── Dispose ───────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
