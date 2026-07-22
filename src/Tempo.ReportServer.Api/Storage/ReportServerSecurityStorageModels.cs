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

/// <summary>
/// EF entity for a just-in-time provisioned report server user. The row is upserted the first time
/// a subject authenticates with a valid OIDC token: identity always lives in Keycloak, this record
/// is a local projection used for ACL subjects and auditing.
/// </summary>
public sealed class ReportServerUserEntity
{
    /// <summary>OIDC subject identifier (<c>sub</c>); primary key.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Tenant identifier the subject was first seen under.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Email address (from the token, may be empty).</summary>
    public string? Email { get; set; }

    /// <summary>Display name (preferred_username / name).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Timestamp of the first authentication.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>Timestamp of the most recent authentication.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>
/// EF entity for a per-folder permission grant. A grant maps an ACL subject (a user <c>sub</c>, a
/// built-in role name, or an embedding application id) to a folder and either an explicit set of
/// permission flags (<see cref="Permissions"/>) or, for legacy rows, a role name projected into
/// permissions. The resolver inherits grants down the folder tree and applies <see cref="Effect"/>
/// (Allow/Deny) with deny winning. Keycloak realm roles remain the capability ceiling.
/// </summary>
public sealed class ReportFolderPermissionEntity
{
    /// <summary>Surrogate identity key.</summary>
    public long Id { get; set; }

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Folder identifier the grant is defined on.</summary>
    public string FolderId { get; set; } = string.Empty;

    /// <summary>Canonical folder path (denormalized for auditing/reporting).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>ACL subject identifier (user <c>sub</c>, role name, or application id).</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Subject kind stored as the integer value of <see cref="Security.ReportAclSubjectKind"/>.
    /// Defaults to <c>0</c> (User) so legacy rows read as user grants.
    /// </summary>
    public int SubjectKind { get; set; }

    /// <summary>
    /// ACL effect stored as the integer value of <see cref="Security.ReportAclEffect"/>.
    /// Defaults to <c>0</c> (Allow) so legacy rows read as allow grants.
    /// </summary>
    public int Effect { get; set; }

    /// <summary>
    /// Explicit permission flags (integer value of <see cref="Security.ReportPermission"/>). When
    /// <see langword="null"/> (legacy rows) the store projects <see cref="Role"/> into permissions.
    /// </summary>
    public int? Permissions { get; set; }

    /// <summary>Granted role name: <c>Admin</c>, <c>Author</c> or <c>Viewer</c> (kept for display/back-compat).</summary>
    public string Role { get; set; } = string.Empty;
}
