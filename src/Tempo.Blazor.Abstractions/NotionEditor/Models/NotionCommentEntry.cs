namespace Tempo.Blazor.NotionEditor.Models;

public class NotionCommentEntry : INotionCommentEntry
{
    public Guid Id { get; set; }
    public Guid? ParentEntryId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public List<ICommentReaction> Reactions { get; set; } = new();

    IReadOnlyList<ICommentReaction> INotionCommentEntry.Reactions => Reactions;
}
