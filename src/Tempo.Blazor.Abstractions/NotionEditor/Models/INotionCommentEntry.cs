namespace Tempo.Blazor.NotionEditor.Models;

public interface INotionCommentEntry
{
    Guid Id { get; }
    Guid? ParentEntryId { get; }
    string AuthorUserId { get; }
    string AuthorDisplayName { get; }
    string? AuthorAvatarUrl { get; }
    string HtmlContent { get; }
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
    bool CanEdit { get; }
    bool CanDelete { get; }

    /// <summary>Emoji reactions on this entry.</summary>
    IReadOnlyList<ICommentReaction> Reactions { get; }
}
