namespace Tempo.Blazor.Models;

/// <summary>An emoji reaction on a chat message, grouped by emoji with the reacting users.</summary>
public sealed record ChatReaction
{
    /// <summary>The emoji (e.g. "👍", "❤️").</summary>
    public string Emoji { get; init; } = string.Empty;

    /// <summary>Users who reacted with this emoji.</summary>
    public IReadOnlyList<ChatUser> Users { get; init; } = [];

    /// <summary>Number of users who reacted with this emoji.</summary>
    public int Count => Users.Count;

    public ChatReaction() { }

    public ChatReaction(string emoji, IReadOnlyList<ChatUser>? users = null)
    {
        Emoji = emoji;
        Users = users ?? [];
    }

    /// <summary>Returns true when <paramref name="userId"/> is among the reacting users.</summary>
    public bool ReactedBy(string userId)
        => Users.Any(u => string.Equals(u.Id, userId, StringComparison.Ordinal));
}
