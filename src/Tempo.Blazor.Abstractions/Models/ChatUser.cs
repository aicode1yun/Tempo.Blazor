namespace Tempo.Blazor.Models;

/// <summary>
/// Represents a user participating in a chat conversation.
/// </summary>
public sealed record ChatUser
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional avatar URL or initials fallback.</summary>
    public string? Avatar { get; init; }

    /// <summary>Online status indicator.</summary>
    public bool IsOnline { get; init; }

    /// <summary>Custom status text (e.g. "Typing…", "Away").</summary>
    public string? Status { get; init; }

    public ChatUser() { }

    public ChatUser(string id, string name, string? avatar = null, bool isOnline = false, string? status = null)
    {
        Id = id;
        Name = name;
        Avatar = avatar;
        IsOnline = isOnline;
        Status = status;
    }
}
