namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

public interface INotionBlockProvider
{
    Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId);
    Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId);
    Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId);
    Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId);
    Task UpdateBlockAsync(IPageBlock block);
    Task DeleteBlockAsync(string blockId);
    Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds);
    Task MoveBlockAsync(MoveNotionBlockRequest request);
    Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId);
    Task<IPageBlock> DuplicateBlockAsync(string blockId);
    Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType);

    /// <summary>
    /// Writes blocks back exactly as they were, ids and parent links included. Undo of a delete
    /// depends on it: recreating the blocks would hand them new ids, so a restored container would
    /// lose the children that still point at its old id.
    /// Providers that do not override this fall back to creating the blocks afresh.
    /// </summary>
    Task RestoreBlocksAsync(IEnumerable<IPageBlock> blocks)
    {
        var first = blocks.FirstOrDefault();
        return first is null
            ? Task.CompletedTask
            : CreateBlocksAsync(first.PageId.ToString(), blocks, null);
    }

    /// <summary>
    /// Converts a block, using the caller's live editor HTML instead of the stored content.
    /// The editor passes the contenteditable's current value so that text typed since the last
    /// blur is not lost. Providers that do not override this ignore <paramref name="currentHtml"/>.
    /// </summary>
    Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType, string? currentHtml)
        => ConvertBlockTypeAsync(blockId, newType);

    Task<string> GetBlockLinkAsync(string blockId);
}
