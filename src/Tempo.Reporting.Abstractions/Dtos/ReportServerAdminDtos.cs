#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Dtos;

/// <summary>
/// Report server permission bits exposed over the wire. Mirrors the server-side
/// <c>Tempo.ReportServer.Api.Security.ReportPermission</c> flags exactly (same bit layout) so an
/// admin client can display and request API-key scopes and folder ACL grants without referencing the
/// server assembly.
/// </summary>
[Flags]
public enum ReportPermissionsDto
{
    /// <summary>No permission.</summary>
    None = 0,

    /// <summary>Can see catalog metadata.</summary>
    View = 1 << 0,

    /// <summary>Can render reports.</summary>
    Render = 1 << 1,

    /// <summary>Can export report output.</summary>
    Export = 1 << 2,

    /// <summary>Can create or update report definitions.</summary>
    EditDefinition = 1 << 3,

    /// <summary>Can manage tenant data sources.</summary>
    ManageDataSources = 1 << 4,

    /// <summary>Can change folder ACL entries.</summary>
    ManagePermissions = 1 << 5,

    /// <summary>All current permissions.</summary>
    All = View | Render | Export | EditDefinition | ManageDataSources | ManagePermissions,
}

/// <summary>Subject type used by folder ACL grants (mirrors the server ACL subject kind).</summary>
public enum ReportAclSubjectKindDto
{
    /// <summary>User identifier.</summary>
    User,

    /// <summary>Built-in role name.</summary>
    Role,

    /// <summary>Embedding application identifier.</summary>
    Application,
}

/// <summary>ACL effect (mirrors the server ACL effect).</summary>
public enum ReportAclEffectDto
{
    /// <summary>Allow the selected permissions.</summary>
    Allow,

    /// <summary>Deny the selected permissions.</summary>
    Deny,
}

/// <summary>Audited report server action (mirrors the server audit action).</summary>
public enum ReportAuditActionDto
{
    /// <summary>Report rendering.</summary>
    RenderReport,

    /// <summary>Report export.</summary>
    ExportReport,

    /// <summary>Report definition or revision change.</summary>
    ChangeDefinition,

    /// <summary>Data source change.</summary>
    ChangeDataSource,

    /// <summary>ACL change.</summary>
    ChangeAcl,
}

/// <summary>Audit outcome (mirrors the server audit outcome).</summary>
public enum ReportAuditOutcomeDto
{
    /// <summary>Operation was allowed.</summary>
    Allowed,

    /// <summary>Operation was denied.</summary>
    Denied,
}

/// <summary>Resource category protected by report server authorization (mirrors the server resource kind).</summary>
public enum ReportResourceKindDto
{
    /// <summary>Folder resource.</summary>
    Folder,

    /// <summary>Report definition or revision resource.</summary>
    ReportDefinition,

    /// <summary>Report render operation.</summary>
    Render,

    /// <summary>Report export operation.</summary>
    Export,

    /// <summary>Tenant data source resource.</summary>
    DataSource,

    /// <summary>Folder ACL resource.</summary>
    Acl,
}

/// <summary>Embedding API key descriptor projected for admin clients. Never carries secret material.</summary>
public sealed record ReportApiKeyDto
{
    /// <summary>Key identifier.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Embedding application identifier (machine principal / service account).</summary>
    public string ApplicationId { get; init; } = string.Empty;

    /// <summary>Allowed operation scopes.</summary>
    public ReportPermissionsDto Permissions { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Optional expiration timestamp.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Revocation timestamp, if the key has been revoked.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>User that revoked the key.</summary>
    public string? RevokedByUserId { get; init; }

    /// <summary>Whether the key is currently active (neither revoked nor expired).</summary>
    public bool IsActive { get; init; }
}

/// <summary>Request to create a tenant/application-scoped API key.</summary>
public sealed record CreateReportApiKeyRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Embedding application identifier.</summary>
    public string ApplicationId { get; init; } = string.Empty;

    /// <summary>Granted operation scopes.</summary>
    public ReportPermissionsDto Permissions { get; init; } = ReportPermissionsDto.View | ReportPermissionsDto.Render;

    /// <summary>Optional expiration timestamp.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// API key creation/rotation result. The <see cref="PlainTextKey"/> is returned exactly once and is
/// never retrievable again — the store keeps only a hash.
/// </summary>
public sealed record CreateReportApiKeyResultDto
{
    /// <summary>New key identifier.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>One-time plain-text secret. Callers must persist it immediately.</summary>
    public string PlainTextKey { get; init; } = string.Empty;

    /// <summary>Stored descriptor for the new key.</summary>
    public ReportApiKeyDto Key { get; init; } = new();
}

/// <summary>Request to rotate an API key (revoke the old key, issue a replacement with the same scope).</summary>
public sealed record RotateReportApiKeyRequestDto
{
    /// <summary>Tenant identifier owning the key.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Optional expiration timestamp for the replacement key.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>Request to revoke an API key.</summary>
public sealed record RevokeReportApiKeyRequestDto
{
    /// <summary>Tenant identifier owning the key.</summary>
    public string TenantId { get; init; } = string.Empty;
}

/// <summary>Report server audit event projected for admin clients.</summary>
public sealed record ReportAuditEventDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User id or API actor id.</summary>
    public string ActorId { get; init; } = string.Empty;

    /// <summary>Audited action.</summary>
    public ReportAuditActionDto Action { get; init; }

    /// <summary>Resource kind.</summary>
    public ReportResourceKindDto ResourceKind { get; init; }

    /// <summary>Resource identifier.</summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>Operation outcome.</summary>
    public ReportAuditOutcomeDto Outcome { get; init; }

    /// <summary>Event timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Optional details.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Per-folder ACL grant projected for admin clients.</summary>
public sealed record ReportFolderAclEntryDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier where the entry is defined.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Subject kind.</summary>
    public ReportAclSubjectKindDto SubjectKind { get; init; }

    /// <summary>User id, role name or application id.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>Allow or deny effect.</summary>
    public ReportAclEffectDto Effect { get; init; }

    /// <summary>Permission flags affected by this entry.</summary>
    public ReportPermissionsDto Permissions { get; init; }
}

/// <summary>Request to grant (or replace) an ACL entry for a subject on a folder.</summary>
public sealed record GrantReportPermissionRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier the grant applies to.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Subject kind.</summary>
    public ReportAclSubjectKindDto SubjectKind { get; init; } = ReportAclSubjectKindDto.User;

    /// <summary>User id, role name or application id.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>Allow or deny effect.</summary>
    public ReportAclEffectDto Effect { get; init; } = ReportAclEffectDto.Allow;

    /// <summary>Permission flags to grant.</summary>
    public ReportPermissionsDto Permissions { get; init; } = ReportPermissionsDto.View | ReportPermissionsDto.Render;
}

/// <summary>Request to revoke a subject's ACL grant on a folder.</summary>
public sealed record RevokeReportPermissionRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier the grant applies to.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Subject kind.</summary>
    public ReportAclSubjectKindDto SubjectKind { get; init; } = ReportAclSubjectKindDto.User;

    /// <summary>User id, role name or application id.</summary>
    public string SubjectId { get; init; } = string.Empty;
}

/// <summary>
/// Resolved report catalog entry returned to the viewer. Carries the metadata and current revision a
/// viewer needs to open and render a real report from the catalog, by report id or by path.
/// </summary>
public sealed record ReportResolveResultDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Report name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Latest revision identifier.</summary>
    public string? LatestRevisionId { get; init; }

    /// <summary>Published revision identifier, if a revision has been published.</summary>
    public string? PublishedRevisionId { get; init; }

    /// <summary>Revision number of the resolved (published if any, otherwise latest) revision.</summary>
    public int RevisionNumber { get; init; }

    /// <summary>Canonical report definition JSON of the resolved revision.</summary>
    public string DefinitionJson { get; init; } = string.Empty;

    /// <summary>Relative render endpoint path the viewer should POST a render request to.</summary>
    public string RenderPath { get; init; } = "api/render";
}

#pragma warning restore MA0016, MA0048
