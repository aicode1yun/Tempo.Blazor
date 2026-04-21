namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class NotionSearchFilter
{
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? CreatedByUserId { get; set; }
    public BlockType? BlockType { get; set; }
    public Guid? InPageId { get; set; }
}
