namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Lightweight resource snapshot embedded in scheduling and workflow models.</summary>
public sealed class TmResourceRef : IEquatable<TmResourceRef>
{
    private static readonly StringComparer ScopeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;

    /// <summary>Stable resource identifier within the source and tenant scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name captured at the time the reference was created.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional resource type, for example <c>person</c>, <c>team</c>, <c>room</c>, or <c>equipment</c>.</summary>
    public string? ResourceType { get; set; }

    /// <summary>Optional color token or sanitized CSS color used for resource labels and timelines.</summary>
    public string? Color { get; set; }

    /// <summary>Optional provider/source discriminator for applications with multiple resource sources.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Returns true when the required identity fields are populated.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Id);

    /// <summary>Creates a reference snapshot from a full resource model.</summary>
    /// <param name="resource">Resource to copy.</param>
    public static TmResourceRef FromResource(TmResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource.ToRef();
    }

    /// <inheritdoc />
    public bool Equals(TmResourceRef? other)
        => other is not null
        && ScopeComparer.Equals(TenantId, other.TenantId)
        && ScopeComparer.Equals(SourceKey, other.SourceKey)
        && IdComparer.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TmResourceRef other && Equals(other);

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
