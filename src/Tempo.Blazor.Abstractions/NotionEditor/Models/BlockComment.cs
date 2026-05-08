namespace Tempo.Blazor.NotionEditor.Models;

public class BlockComment : IBlockComment
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public IReadOnlyList<INotionCommentEntry> Thread { get; set; } = new List<INotionCommentEntry>();
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }

    /// <summary>User IDs who have marked this thread as read/dismissed.</summary>
    public List<string> ReadByUserIds { get; set; } = new();

    /// <summary>Timestamp of the most recent activity (new entry, reaction, resolve).</summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>User IDs subscribed to this thread (receive notifications).</summary>
    public List<string> SubscribedUserIds { get; set; } = new();

    IReadOnlyList<string> IBlockComment.ReadByUserIds => ReadByUserIds;
    IReadOnlyList<string> IBlockComment.SubscribedUserIds => SubscribedUserIds;
}
