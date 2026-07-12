namespace Tempo.Blazor.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Message text content.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Author of the message.</summary>
    public ChatUser? Author { get; init; }

    /// <summary>Timestamp when the message was sent.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Message direction / type.</summary>
    public ChatMessageType Type { get; init; } = ChatMessageType.Incoming;

    /// <summary>Optional attachments.</summary>
    public IReadOnlyList<ChatAttachment> Attachments { get; init; } = [];

    /// <summary>Whether the message has been read by the recipient.</summary>
    public bool IsRead { get; init; }

    /// <summary>Whether the message is currently being sent.</summary>
    public bool IsSending { get; init; }

    /// <summary>Whether the message failed to send.</summary>
    public bool IsError { get; init; }

    /// <summary>Optional error text when sending failed.</summary>
    public string? ErrorMessage { get; init; }

    // ── K6: threads, edit/delete, reactions, receipts (all additive) ──

    /// <summary>Id of the specific message this one replies to, when it is a reply.</summary>
    public string? ReplyToId { get; init; }

    /// <summary>Id of the root message of the thread this message belongs to; the root's own
    /// <see cref="Id"/> for a thread starter, or the root id for its replies. Null for standalone messages.</summary>
    public string? ThreadRootId { get; init; }

    /// <summary>Number of replies in this message's thread (meaningful on a thread root).</summary>
    public int ReplyCount { get; init; }

    /// <summary>Emoji reactions on this message, grouped by emoji.</summary>
    public IReadOnlyList<ChatReaction> Reactions { get; init; } = [];

    /// <summary>Per-user read receipts for this message.</summary>
    public IReadOnlyList<ChatReadReceipt> ReadBy { get; init; } = [];

    /// <summary>Timestamp of the last edit, or null when the message was never edited.</summary>
    public DateTimeOffset? EditedAt { get; init; }

    /// <summary>Whether the message has been deleted (rendered as a tombstone).</summary>
    public bool IsDeleted { get; init; }

    /// <summary>True when the message carries an edit timestamp.</summary>
    public bool IsEdited => EditedAt.HasValue;

    /// <summary>True when the message is a reply within a thread.</summary>
    public bool IsReply => !string.IsNullOrEmpty(ReplyToId) || !string.IsNullOrEmpty(ThreadRootId);

    /// <summary>Returns true when <paramref name="userId"/> has read this message.</summary>
    public bool IsReadByUser(string userId)
        => IsRead || ReadBy.Any(r => string.Equals(r.User?.Id, userId, StringComparison.Ordinal));

    public ChatMessage() { }

    public ChatMessage(string id, string text, ChatUser? author = null, ChatMessageType type = ChatMessageType.Incoming, DateTimeOffset? timestamp = null)
    {
        Id = id;
        Text = text;
        Author = author;
        Type = type;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Direction / type of a chat message.
/// </summary>
public enum ChatMessageType
{
    /// <summary>Message received from another user.</summary>
    Incoming,

    /// <summary>Message sent by the current user.</summary>
    Outgoing,

    /// <summary>System notification (e.g. "User joined", "User left").</summary>
    System
}
