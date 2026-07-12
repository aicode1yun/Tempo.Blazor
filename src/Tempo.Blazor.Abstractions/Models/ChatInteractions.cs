namespace Tempo.Blazor.Models;

/// <summary>Payload for sending a chat message, optionally as a threaded reply.</summary>
public sealed record ChatSendRequest
{
    /// <summary>The message text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Id of the specific message being replied to, when this is a reply.</summary>
    public string? ReplyToId { get; init; }

    /// <summary>Id of the thread root this message belongs to, when sent inside a thread.</summary>
    public string? ThreadRootId { get; init; }

    public ChatSendRequest() { }

    public ChatSendRequest(string text, string? replyToId = null, string? threadRootId = null)
    {
        Text = text;
        ReplyToId = replyToId;
        ThreadRootId = threadRootId;
    }
}

/// <summary>Payload for editing a chat message's text.</summary>
public sealed record ChatMessageEdit(string MessageId, string NewText);

/// <summary>Payload for toggling an emoji reaction on a chat message.</summary>
public sealed record ChatReactionToggle(string MessageId, string Emoji);
