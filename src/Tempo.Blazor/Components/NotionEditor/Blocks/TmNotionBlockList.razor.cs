using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Page;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks;

/// <summary>
/// Renders an ordered list of page blocks, manages drag-and-drop reordering via JS interop,
/// and surfaces per-block events up to the parent page component.
/// </summary>
public partial class TmNotionBlockList : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IReadOnlyList<IPageBlock> Blocks { get; set; } = [];

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public Guid? ActiveBlockId { get; set; }

    /// <summary>Per-block comment summary for margin-thread indicators and hover tooltip.</summary>
    [Parameter] public IReadOnlyDictionary<string, TmNotionPage.BlockCommentInfo> BlockCommentCounts { get; set; } = new Dictionary<string, TmNotionPage.BlockCommentInfo>();

    /// <summary>Fired when blocks are reordered via drag-and-drop (sourceIndex, targetIndex).</summary>
    [Parameter] public EventCallback<(int, int)> OnReorder { get; set; }

    /// <summary>Fired when a block receives focus. Arg is the block ID string.</summary>
    [Parameter] public EventCallback<string> OnBlockFocused { get; set; }

    /// <summary>Fired when a block requests deletion. Arg is the block ID string.</summary>
    [Parameter] public EventCallback<string> OnBlockDeleted { get; set; }

    /// <summary>Fired when a block's content was saved by the user.</summary>
    [Parameter] public EventCallback<IPageBlock> OnBlockUpdated { get; set; }

    /// <summary>Fired when a block should be duplicated (carries the source block).</summary>
    [Parameter] public EventCallback<IPageBlock> OnBlockDuplicated { get; set; }

    /// <summary>Fired when a block requests type conversion (blockId, newType).</summary>
    [Parameter] public EventCallback<(string, BlockType)> OnConvertBlock { get; set; }

    /// <summary>Fired when a new block should be inserted after the given block (afterBlockId, type, initialHtml).</summary>
    [Parameter] public EventCallback<(string AfterBlockId, BlockType Type, string? InitialHtml)> OnAddBlockAfter { get; set; }

    /// <summary>Fired when the user clicks the empty area below all blocks.</summary>
    [Parameter] public EventCallback OnAddBlockAtEnd { get; set; }

    /// <summary>Fired when a block's '/' keystroke opens the slash menu (blockId, top, left).</summary>
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnSlashMenu { get; set; }

    /// <summary>Fired when '@' mention syntax is typed in a block (blockId, top, left).</summary>
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnMentionMenu { get; set; }

    /// <summary>Fired when '[[' page-link syntax is typed in a block (blockId, top, left).</summary>
    [Parameter] public EventCallback<(string BlockId, double Top, double Left)> OnPageLinkMenu { get; set; }

    /// <summary>Fired when a template button block inserts its template blocks after itself.</summary>
    [Parameter] public EventCallback<(string AfterBlockId, IReadOnlyList<IPageBlock> Blocks)> OnInsertTemplateBlocksAfter { get; set; }

    /// <summary>Fired when a block's comment button is clicked. Arg is the block ID string.</summary>
    [Parameter] public EventCallback<string> OnComment { get; set; }

    /// <summary>Fired when a block's new-thread button is clicked. Arg is the block ID string.</summary>
    [Parameter] public EventCallback<string> OnNewThread { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                             _listRef;
    private DotNetObjectReference<TmNotionBlockList>?   _dotNetRef;
    private bool                                         _dragInitialized;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ReadOnly)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.initDragDrop", _listRef, _dotNetRef);
                _dragInitialized = true;
            }
            catch { /* SSR / test */ }
        }
    }

    // ── JS callbacks ─────────────────────────────────────────────────────────

    [JSInvokable]
    public async Task OnBlockReordered(int sourceIndex, int targetIndex) =>
        await OnReorder.InvokeAsync((sourceIndex, targetIndex));

    // ── Comment info helper ──────────────────────────────────────────────────

    internal TmNotionPage.BlockCommentInfo GetBlockCommentInfo(string blockId)
        => BlockCommentCounts.TryGetValue(blockId, out var info) ? info : new TmNotionPage.BlockCommentInfo(0, 0, false, null, null, null, null, 0);

    // ── Numbering helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Computes the 1-based ordinal for a NumberedList block at <paramref name="blockIndex"/>.
    /// Counts preceding NumberedList siblings at the same indent level; child-level blocks
    /// (higher indent) are skipped, a parent-level block or non-list block terminates the search.
    /// Returns 0 for non-NumberedList blocks.
    /// </summary>
    internal int GetNumberedListNumber(int blockIndex)
    {
        var block = Blocks[blockIndex];
        if (block.Type != BlockType.NumberedList) return 0;
        var indent = (block.Content as IListBlockContent)?.IndentLevel ?? 0;
        var count = 1;
        for (var i = blockIndex - 1; i >= 0; i--)
        {
            var prev = Blocks[i];
            if (prev.Type != BlockType.NumberedList) break;
            var prevIndent = (prev.Content as IListBlockContent)?.IndentLevel ?? 0;
            if (prevIndent < indent) break;       // parent level — our sequence didn't exist before
            if (prevIndent == indent) count++;    // same level — count it
            // prevIndent > indent → child level → skip
        }
        return count;
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_dragInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyDragDrop", _listRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
