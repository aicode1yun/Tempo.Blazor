namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Snapshot of the current user and their group memberships.</summary>
public sealed class TmCurrentUserState
{
    /// <summary>Current user, or null when the caller is anonymous.</summary>
    public TmUserRef? User { get; set; }

    /// <summary>Group identifiers associated with the current user.</summary>
    public IReadOnlyList<string> GroupIds { get; set; } = [];

    /// <summary>Returns true when a valid current user is available.</summary>
    public bool IsAuthenticated => User?.IsValid == true;

    /// <summary>Anonymous current-user state.</summary>
    public static TmCurrentUserState Anonymous => new();

    /// <summary>Creates an authenticated current-user state from a user reference.</summary>
    /// <param name="user">Current user reference.</param>
    /// <param name="groupIds">Optional current user group ids.</param>
    public static TmCurrentUserState FromUser(TmUserRef user, IEnumerable<string>? groupIds = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new TmCurrentUserState
        {
            User = user,
            GroupIds = groupIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? []
        };
    }
}
