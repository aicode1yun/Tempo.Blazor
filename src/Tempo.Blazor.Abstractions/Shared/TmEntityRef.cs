namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>
/// Stable reference to an entity that can be targeted by shared concepts such as
/// comments, attachments, activity entries, and notifications.
/// </summary>
/// <remarks>
/// Equality is based on identity fields only: <see cref="TenantId"/>,
/// <see cref="SourceKey"/>, <see cref="EntityType"/>, and <see cref="EntityId"/>.
/// <see cref="DisplayName"/> and <see cref="Url"/> are descriptive metadata and do
/// not participate in equality.
/// </remarks>
public sealed class TmEntityRef : IEquatable<TmEntityRef>
{
    private static readonly StringComparer ScopeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;

    /// <summary>Logical entity type, for example <c>work-item</c>, <c>page</c>, or <c>document</c>.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Stable identifier of the entity within the source and tenant scope.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Optional provider/source discriminator, matching provider keys such as <c>TmWorkItem.SourceKey</c>.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional display name captured for UI hints and logs.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Optional URL that opens the entity in its owning application.</summary>
    public string? Url { get; set; }

    /// <summary>Returns true when no identity fields have been populated.</summary>
    public bool IsEmpty
        => string.IsNullOrWhiteSpace(EntityType)
        && string.IsNullOrWhiteSpace(EntityId)
        && string.IsNullOrWhiteSpace(SourceKey)
        && string.IsNullOrWhiteSpace(TenantId);

    /// <summary>Returns true when the required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(EntityType)
        && !string.IsNullOrWhiteSpace(EntityId);

    /// <summary>Creates a normalized entity reference and validates required fields.</summary>
    /// <param name="entityType">Logical entity type.</param>
    /// <param name="entityId">Stable entity identifier.</param>
    /// <param name="sourceKey">Optional provider/source discriminator.</param>
    /// <param name="tenantId">Optional tenant or workspace scope.</param>
    /// <param name="displayName">Optional UI display name.</param>
    /// <param name="url">Optional URL for opening the entity.</param>
    public static TmEntityRef Create(
        string entityType,
        string entityId,
        string? sourceKey = null,
        string? tenantId = null,
        string? displayName = null,
        string? url = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity id is required.", nameof(entityId));

        return new TmEntityRef
        {
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            SourceKey = CleanOptional(sourceKey),
            TenantId = CleanOptional(tenantId),
            DisplayName = CleanOptional(displayName),
            Url = CleanOptional(url)
        };
    }

    /// <summary>Returns a copy with whitespace trimmed and blank optional values converted to null.</summary>
    public TmEntityRef Normalize()
        => new()
        {
            EntityType = CleanRequired(EntityType),
            EntityId = CleanRequired(EntityId),
            SourceKey = CleanOptional(SourceKey),
            TenantId = CleanOptional(TenantId),
            DisplayName = CleanOptional(DisplayName),
            Url = CleanOptional(Url)
        };

    /// <summary>
    /// Returns a stable key suitable for diagnostics, caches, and tests. The key is
    /// scoped by tenant and source when present.
    /// </summary>
    public string ToQualifiedKey()
    {
        var normalized = Normalize();
        if (!normalized.IsValid)
            return string.Empty;

        var parts = new List<string>(4);
        if (!string.IsNullOrEmpty(normalized.TenantId))
            parts.Add($"tenant:{normalized.TenantId}");
        if (!string.IsNullOrEmpty(normalized.SourceKey))
            parts.Add($"source:{normalized.SourceKey}");

        parts.Add($"type:{normalized.EntityType}");
        parts.Add($"id:{normalized.EntityId}");

        return string.Join("|", parts);
    }

    /// <inheritdoc />
    public bool Equals(TmEntityRef? other)
        => other is not null
        && ScopeComparer.Equals(TenantId, other.TenantId)
        && ScopeComparer.Equals(SourceKey, other.SourceKey)
        && ScopeComparer.Equals(EntityType, other.EntityType)
        && IdComparer.Equals(EntityId, other.EntityId);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TmEntityRef other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId, ScopeComparer);
        hash.Add(SourceKey, ScopeComparer);
        hash.Add(EntityType, ScopeComparer);
        hash.Add(EntityId, IdComparer);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var key = ToQualifiedKey();
        return string.IsNullOrWhiteSpace(DisplayName)
            ? key
            : $"{DisplayName} ({key})";
    }

    private static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static string CleanRequired(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
