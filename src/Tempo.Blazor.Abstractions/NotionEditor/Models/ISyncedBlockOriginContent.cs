namespace Tempo.Blazor.NotionEditor.Models;

public interface ISyncedBlockOriginContent : IBlockContent
{
    Guid SyncId { get; }
}
