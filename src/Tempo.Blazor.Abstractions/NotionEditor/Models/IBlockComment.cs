namespace Tempo.Blazor.NotionEditor.Models;

public interface IBlockComment
{
    Guid Id { get; }
    Guid BlockId { get; }
    IReadOnlyList<INotionCommentEntry> Thread { get; }
    bool IsResolved { get; }
    DateTime? ResolvedAt { get; }
    string? ResolvedByUserId { get; }
}
