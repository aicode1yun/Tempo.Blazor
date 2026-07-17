namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// EF entity for a persisted embedding API key. Only the SHA-256 hash of the plain text key is
/// stored (<see cref="KeyHash"/>); the plain text is shown once at creation and never persisted.
/// </summary>
public sealed class ReportApiKeyEntity
{
    /// <summary>Stable key identifier (e.g. <c>rk_...</c>).</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Embedding application identifier (machine principal / service account).</summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>Base64-encoded SHA-256 hash of the plain text key.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Permission scope flags granted by the key.</summary>
    public int Permissions { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optional expiration timestamp.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Revocation timestamp; non-null once revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>User or principal that revoked the key.</summary>
    public string? RevokedByUserId { get; set; }
}

/// <summary>EF entity for a persisted report server audit event.</summary>
public sealed class ReportAuditEventEntity
{
    /// <summary>Surrogate identity key.</summary>
    public long Id { get; set; }

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Actor identifier (user id or API principal).</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Audited action.</summary>
    public int Action { get; set; }

    /// <summary>Resource kind.</summary>
    public int ResourceKind { get; set; }

    /// <summary>Resource identifier (e.g. report id or folder id).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Operation outcome.</summary>
    public int Outcome { get; set; }

    /// <summary>Event timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>JSON serialized detail dictionary (parameters and additional metadata).</summary>
    public string DetailsJson { get; set; } = "{}";
}
