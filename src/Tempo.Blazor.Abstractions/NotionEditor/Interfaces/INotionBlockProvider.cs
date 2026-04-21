namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Enums;

public interface INotionBlockProvider
{
    Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId);
    Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId);
    Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId);
    Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId);
    Task UpdateBlockAsync(IPageBlock block);
    Task DeleteBlockAsync(string blockId);
    Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds);
    Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId);
    Task<IPageBlock> DuplicateBlockAsync(string blockId);
    Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType);
    Task<string> GetBlockLinkAsync(string blockId);
}
