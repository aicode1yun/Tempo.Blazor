namespace Tempo.Blazor.NotionEditor.Models;

public class PageComment : IPageComment
{
    public Guid Id { get; set; }
    public string PageId { get; set; } = string.Empty;
    public IReadOnlyList<INotionCommentEntry> Thread { get; set; } = new List<INotionCommentEntry>();
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
