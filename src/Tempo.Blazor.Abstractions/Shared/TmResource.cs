namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Canonical schedulable resource shared across Tempo.Blazor components.</summary>
public sealed class TmResource : IEquatable<TmResource>
{
    private static readonly StringComparer ScopeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;

    /// <summary>Stable resource identifier within the source and tenant scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name shown in user-facing UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional resource type, for example <c>person</c>, <c>team</c>, <c>room</c>, or <c>equipment</c>.</summary>
    public string? ResourceType { get; set; }

    /// <summary>Optional color token or sanitized CSS color used for resource labels and timelines.</summary>
    public string? Color { get; set; }

    /// <summary>Optional group identifier for nested grouping.</summary>
    public string? GroupId { get; set; }

    /// <summary>Display order within the group.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional provider/source discriminator for applications with multiple resource sources.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when the required identity fields are populated.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Id);

    /// <summary>Creates a lightweight reference snapshot from this resource.</summary>
    public TmResourceRef ToRef()
        => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            ResourceType = ResourceType,
            Color = Color,
            SourceKey = SourceKey,
            TenantId = TenantId
        };

    /// <summary>Creates a full resource model from a lightweight reference snapshot.</summary>
    /// <param name="resourceRef">Resource reference to copy.</param>
    public static TmResource FromRef(TmResourceRef resourceRef)
    {
        ArgumentNullException.ThrowIfNull(resourceRef);

        return new TmResource
        {
            Id = resourceRef.Id,
            DisplayName = resourceRef.DisplayName,
            ResourceType = resourceRef.ResourceType,
            Color = resourceRef.Color,
            SourceKey = resourceRef.SourceKey,
            TenantId = resourceRef.TenantId
        };
    }

    /// <inheritdoc />
    public bool Equals(TmResource? other)
        => other is not null
        && ScopeComparer.Equals(TenantId, other.TenantId)
        && ScopeComparer.Equals(SourceKey, other.SourceKey)
        && IdComparer.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TmResource other && Equals(other);

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
