namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>
/// Lightweight user snapshot embedded in comments, notifications, activity entries,
/// assignments, and other shared models.
/// </summary>
public class TmUserRef : IEquatable<TmUserRef>
{
    private static readonly StringComparer ScopeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;

    /// <summary>Stable user identifier within the source and tenant scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name captured at the time the reference was created.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional username or mention handle.</summary>
    public string? UserName { get; set; }

    /// <summary>Optional e-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Optional avatar image URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Optional color token or sanitized CSS color used for avatars and timelines.</summary>
    public string? Color { get; set; }

    /// <summary>True when this represents a virtual resource rather than an account-backed user.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Optional provider/source discriminator for applications with multiple people sources.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Returns true when the required identity fields are populated.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Id);

    /// <summary>Creates a reference snapshot from a full user model.</summary>
    /// <param name="user">User to copy.</param>
    public static TmUserRef FromUser(TmUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.ToRef();
    }

    /// <inheritdoc />
    public bool Equals(TmUserRef? other)
        => other is not null
        && ScopeComparer.Equals(TenantId, other.TenantId)
        && ScopeComparer.Equals(SourceKey, other.SourceKey)
        && IdComparer.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TmUserRef other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId, ScopeComparer);
        hash.Add(SourceKey, ScopeComparer);
        hash.Add(Id, IdComparer);
        return hash.ToHashCode();
    }
}
