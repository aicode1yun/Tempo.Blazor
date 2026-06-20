namespace Tempo.Blazor.NotionEditor.Models;

public interface IBlockComment
{
    Guid Id { get; }
    Guid BlockId { get; }
    IReadOnlyList<INotionCommentEntry> Thread { get; }
    bool IsResolved { get; }
    DateTime? ResolvedAt { get; }
    string? ResolvedByUserId { get; }

    /// <summary>User IDs who have marked this thread as read/dismissed.</summary>
    IReadOnlyList<string> ReadByUserIds { get; }

    /// <summary>Timestamp of the most recent activity (new entry, reaction, resolve).</summary>
    DateTime? LastActivityAt { get; }

    /// <summary>User IDs subscribed to this thread (receive notifications).</summary>
    IReadOnlyList<string> SubscribedUserIds { get; }
}
