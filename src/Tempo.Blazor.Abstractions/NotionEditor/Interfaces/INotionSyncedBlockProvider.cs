namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionSyncedBlockProvider
{
    Task<IEnumerable<IPageBlock>> GetSyncedChildBlocksAsync(string syncId);
    Task UpdateSyncedChildBlocksAsync(string syncId, IEnumerable<IPageBlock> children);
    Task<IEnumerable<(string PageId, string BlockId)>> GetAllSyncRefsAsync(string syncId);
    Task<IPageBlock> CreateSyncRefAsync(string syncId, string targetPageId, string? afterBlockId);
    Task<IPageBlock> UnsyncBlockAsync(string blockId);
}
