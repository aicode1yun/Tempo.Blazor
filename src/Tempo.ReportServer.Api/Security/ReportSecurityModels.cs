#pragma warning disable MA0016, MA0048

namespace Tempo.ReportServer.Api.Security;

/// <summary>Built-in report server role.</summary>
public enum ReportServerRole
{
    /// <summary>Can view and render reports.</summary>
    Viewer,

    /// <summary>Can author report definitions.</summary>
    Author,

    /// <summary>Can administer the tenant.</summary>
    TenantAdmin,
}

/// <summary>Report server permission bits.</summary>
[Flags]
public enum ReportPermission
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

/// <summary>Resource category protected by report server authorization.</summary>
public enum ReportResourceKind
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

/// <summary>Subject type used by folder ACL entries.</summary>
public enum ReportAclSubjectKind
{
    /// <summary>User identifier.</summary>
    User,

    /// <summary>Built-in role name.</summary>
    Role,

    /// <summary>Embedding application identifier.</summary>
    Application,
}

/// <summary>ACL effect.</summary>
public enum ReportAclEffect
{
    /// <summary>Allow the selected permissions.</summary>
    Allow,

    /// <summary>Deny the selected permissions.</summary>
    Deny,
}

/// <summary>Folder node used by permission inheritance.</summary>
public sealed record ReportFolderPermissionNode(
    string FolderId,
    string? ParentFolderId = null,
    string TenantId = "");

/// <summary>Per-folder ACL entry.</summary>
public sealed record ReportFolderAclEntry
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier where the entry is defined.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Subject kind.</summary>
    public ReportAclSubjectKind SubjectKind { get; init; }

    /// <summary>User id, role name or application id.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>Allow or deny effect.</summary>
    public ReportAclEffect Effect { get; init; }

    /// <summary>Permission flags affected by this entry.</summary>
    public ReportPermission Permissions { get; init; }

    /// <summary>Creates an allow entry for a user.</summary>
    public static ReportFolderAclEntry AllowUser(string folderId, string userId, ReportPermission permissions)
        => Entry(folderId, ReportAclSubjectKind.User, userId, ReportAclEffect.Allow, permissions);

    /// <summary>Creates a deny entry for a user.</summary>
    public static ReportFolderAclEntry DenyUser(string folderId, string userId, ReportPermission permissions)
        => Entry(folderId, ReportAclSubjectKind.User, userId, ReportAclEffect.Deny, permissions);

    /// <summary>Creates an allow entry for a role.</summary>
    public static ReportFolderAclEntry AllowRole(string folderId, ReportServerRole role, ReportPermission permissions)
        => Entry(folderId, ReportAclSubjectKind.Role, role.ToString(), ReportAclEffect.Allow, permissions);

    /// <summary>Creates a deny entry for a role.</summary>
    public static ReportFolderAclEntry DenyRole(string folderId, ReportServerRole role, ReportPermission permissions)
        => Entry(folderId, ReportAclSubjectKind.Role, role.ToString(), ReportAclEffect.Deny, permissions);

    /// <summary>Creates an allow entry for an embedding application.</summary>
    public static ReportFolderAclEntry AllowApplication(string folderId, string applicationId, ReportPermission permissions)
        => Entry(folderId, ReportAclSubjectKind.Application, applicationId, ReportAclEffect.Allow, permissions);

    private static ReportFolderAclEntry Entry(
        string folderId,
        ReportAclSubjectKind subjectKind,
        string subjectId,
        ReportAclEffect effect,
        ReportPermission permissions)
        => new()
        {
            FolderId = folderId,
            SubjectKind = subjectKind,
            SubjectId = subjectId,
            Effect = effect,
            Permissions = permissions,
        };
}

/// <summary>Authorization requirement attached to a report server endpoint.</summary>
public sealed record ReportPermissionRequirement(
    ReportPermission Permission,
    ReportResourceKind ResourceKind,
    bool RequiresAuthorRole = false,
    string FolderRouteKey = "folderId");

/// <summary>Authentication source used by a report security context.</summary>
public enum ReportAuthenticationKind
{
    /// <summary>User request.</summary>
    User,

    /// <summary>Embedding API key request.</summary>
    ApiKey,
}

/// <summary>Security principal resolved from user claims or an embedding API key.</summary>
public sealed record ReportSecurityContext
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User id or API key actor id.</summary>
    public string ActorId { get; init; } = string.Empty;

    /// <summary>Built-in user roles.</summary>
    public IReadOnlyCollection<ReportServerRole> Roles { get; init; } = [];

    /// <summary>Authentication source.</summary>
    public ReportAuthenticationKind AuthenticationKind { get; init; }

    /// <summary>Embedding application id for API key requests.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Permission scope granted by an API key.</summary>
    public ReportPermission ApiKeyPermissions { get; init; }

    /// <summary>Creates a user security context.</summary>
    public static ReportSecurityContext ForUser(
        string tenantId,
        string actorId,
        IEnumerable<ReportServerRole> roles)
        => new()
        {
            TenantId = tenantId,
            ActorId = actorId,
            Roles = roles.Distinct().ToArray(),
            AuthenticationKind = ReportAuthenticationKind.User,
        };

    /// <summary>Creates an API key security context.</summary>
    public static ReportSecurityContext ForApiKey(ReportApiKeyDescriptor descriptor)
        => new()
        {
            TenantId = descriptor.TenantId,
            ActorId = $"api:{descriptor.ApplicationId}",
            ApplicationId = descriptor.ApplicationId,
            ApiKeyPermissions = descriptor.Permissions,
            AuthenticationKind = ReportAuthenticationKind.ApiKey,
        };

    /// <summary>Returns true when the principal has the role.</summary>
    public bool HasRole(ReportServerRole role)
        => Roles.Contains(role);
}

/// <summary>Authorization result.</summary>
public sealed record ReportAuthorizationResult(bool Allowed, string Reason)
{
    /// <summary>Allowed result.</summary>
    public static ReportAuthorizationResult Allow() => new(true, string.Empty);

    /// <summary>Denied result.</summary>
    public static ReportAuthorizationResult Deny(string reason) => new(false, reason);
}

/// <summary>Header names used by report server dev/test authentication.</summary>
public static class ReportSecurityHeaders
{
    /// <summary>Tenant header.</summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>User id header.</summary>
    public const string UserId = "X-User-Id";

    /// <summary>Comma-separated role header.</summary>
    public const string Roles = "X-Roles";

    /// <summary>Embedding API key header.</summary>
    public const string ApiKey = "X-Api-Key";
}

#pragma warning restore MA0016, MA0048
