namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public class PageVersion : IPageVersion
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? EditedByUserId { get; set; }
    public string EditedByDisplayName { get; set; } = string.Empty;
    public IReadOnlyList<IPageBlock> BlocksSnapshot { get; set; } = new List<IPageBlock>();
    public string? ChangeDescription { get; set; }
}
