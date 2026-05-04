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
