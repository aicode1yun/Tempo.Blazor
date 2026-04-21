namespace Tempo.Blazor.NotionEditor.Models;

public interface ISyncedBlockRefContent : IBlockContent
{
    Guid SyncId { get; }
    Guid OriginPageId { get; }
    Guid OriginBlockId { get; }
}
