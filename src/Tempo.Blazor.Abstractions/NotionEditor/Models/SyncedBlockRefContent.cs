namespace Tempo.Blazor.NotionEditor.Models;

public class SyncedBlockRefContent : ISyncedBlockRefContent
{
    public Guid SyncId { get; set; }
    public Guid OriginPageId { get; set; }
    public Guid OriginBlockId { get; set; }
}
