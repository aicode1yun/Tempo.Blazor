namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Canonical user or virtual person-like resource shared across Tempo.Blazor components.</summary>
public sealed class TmUser : IEquatable<TmUser>
{
    private static readonly StringComparer ScopeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;

    /// <summary>Stable user identifier within the source and tenant scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name shown in user-facing UI.</summary>
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

    /// <summary>Creates a lightweight reference snapshot from this user.</summary>
    public TmUserRef ToRef()
        => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            UserName = UserName,
            Email = Email,
            AvatarUrl = AvatarUrl,
            Color = Color,
            IsVirtual = IsVirtual,
            SourceKey = SourceKey,
            TenantId = TenantId
        };

    /// <summary>Creates a full user model from a lightweight reference snapshot.</summary>
    /// <param name="userRef">User reference to copy.</param>
    public static TmUser FromRef(TmUserRef userRef)
    {
        ArgumentNullException.ThrowIfNull(userRef);

        return new TmUser
        {
            Id = userRef.Id,
            DisplayName = userRef.DisplayName,
            UserName = userRef.UserName,
            Email = userRef.Email,
            AvatarUrl = userRef.AvatarUrl,
            Color = userRef.Color,
            IsVirtual = userRef.IsVirtual,
            SourceKey = userRef.SourceKey,
            TenantId = userRef.TenantId
        };
    }

    /// <inheritdoc />
    public bool Equals(TmUser? other)
        => other is not null
        && ScopeComparer.Equals(TenantId, other.TenantId)
        && ScopeComparer.Equals(SourceKey, other.SourceKey)
        && IdComparer.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TmUser other && Equals(other);

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
