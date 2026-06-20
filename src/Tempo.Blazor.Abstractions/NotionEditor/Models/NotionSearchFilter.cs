namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class NotionSearchFilter
{
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? LastEditedAfter { get; set; }
    public DateTime? LastEditedBefore { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? Author { get; set; }
    public string? LabelFilter { get; set; }
    public string? ContentType { get; set; }
    public string? SpaceId { get; set; }
    public BlockType? BlockType { get; set; }
    public Guid? InPageId { get; set; }
}
