namespace Tempo.Blazor.Models;

/// <summary>A per-user read receipt for a chat message: who read it and when.</summary>
public sealed record ChatReadReceipt
{
    /// <summary>The user who read the message.</summary>
    public ChatUser? User { get; init; }

    /// <summary>When the user read the message.</summary>
    public DateTimeOffset ReadAt { get; init; } = DateTimeOffset.UtcNow;

    public ChatReadReceipt() { }

    public ChatReadReceipt(ChatUser user, DateTimeOffset? readAt = null)
    {
        User = user;
        ReadAt = readAt ?? DateTimeOffset.UtcNow;
    }
}
