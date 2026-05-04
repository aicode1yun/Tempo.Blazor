namespace Tempo.Blazor.NotionEditor.Models;

public interface IPageComment
{
    Guid Id { get; }
    string PageId { get; }
    IReadOnlyList<INotionCommentEntry> Thread { get; }
    bool IsResolved { get; }
    DateTime? ResolvedAt { get; }
    string? ResolvedByUserId { get; }
}
