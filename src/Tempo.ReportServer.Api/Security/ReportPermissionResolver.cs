#pragma warning disable MA0048

using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Security;

/// <summary>Authorizes report server principals against roles, API key scopes and folder ACLs.</summary>
public interface IReportPermissionResolver
{
    /// <summary>Authorizes a requirement against an optional folder.</summary>
    Task<ReportAuthorizationResult> AuthorizeAsync(
        ReportSecurityContext principal,
        ReportPermissionRequirement requirement,
        string? folderId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default report server permission resolver.</summary>
public sealed class ReportPermissionResolver : IReportPermissionResolver
{
    private readonly IReportPermissionStore _store;

    /// <summary>Creates a resolver.</summary>
    public ReportPermissionResolver(IReportPermissionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<ReportAuthorizationResult> AuthorizeAsync(
        ReportSecurityContext principal,
        ReportPermissionRequirement requirement,
        string? folderId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(requirement);
        cancellationToken.ThrowIfCancellationRequested();

        // User-side author gate: a user principal without the Author or TenantAdmin realm role cannot
        // satisfy an author-only requirement, regardless of any folder ACL grant. Machine (API key)
        // principals have no realm role — their explicit permission scope IS their authority, so they
        // are governed purely by the permission bits below (base scope & folder ACL), not this gate.
        if (requirement.RequiresAuthorRole &&
            principal.AuthenticationKind == ReportAuthenticationKind.User &&
            !principal.HasRole(ReportServerRole.Author) &&
            !principal.HasRole(ReportServerRole.TenantAdmin))
        {
            return ReportAuthorizationResult.Deny("Author role is required.");
        }

        var permissions = BasePermissions(principal);
        var entries = await _store.ListInheritedAclEntriesAsync(
            folderId,
            new ReportExecutionContext(principal.TenantId, principal.ActorId, "en-US", CancellationToken: cancellationToken)).ConfigureAwait(false);
        var allow = ReportPermission.None;
        var deny = ReportPermission.None;
        foreach (var entry in entries.Where(entry => AppliesTo(entry, principal)))
        {
            if (entry.Effect == ReportAclEffect.Deny)
            {
                deny |= entry.Permissions;
            }
            else
            {
                allow |= entry.Permissions;
            }
        }

        var effective = (permissions | allow) & ~deny;
        return Has(effective, requirement.Permission)
            ? ReportAuthorizationResult.Allow()
            : ReportAuthorizationResult.Deny($"Missing permission '{requirement.Permission}'.");
    }

    private static ReportPermission BasePermissions(ReportSecurityContext principal)
    {
        if (principal.AuthenticationKind == ReportAuthenticationKind.ApiKey)
        {
            return principal.ApiKeyPermissions;
        }

        var permissions = ReportPermission.None;
        if (principal.HasRole(ReportServerRole.Viewer))
        {
            permissions |= ReportPermission.View | ReportPermission.Render | ReportPermission.Export;
        }

        if (principal.HasRole(ReportServerRole.Author))
        {
            permissions |= ReportPermission.View | ReportPermission.Render | ReportPermission.Export | ReportPermission.EditDefinition;
        }

        if (principal.HasRole(ReportServerRole.TenantAdmin))
        {
            permissions |= ReportPermission.All;
        }

        return permissions;
    }

    private static bool AppliesTo(ReportFolderAclEntry entry, ReportSecurityContext principal)
        => entry.SubjectKind switch
        {
            ReportAclSubjectKind.User => string.Equals(entry.SubjectId, principal.ActorId, StringComparison.Ordinal),
            ReportAclSubjectKind.Application => string.Equals(entry.SubjectId, principal.ApplicationId, StringComparison.Ordinal),
            ReportAclSubjectKind.Role => principal.Roles.Any(role => string.Equals(entry.SubjectId, role.ToString(), StringComparison.Ordinal)),
            _ => false,
        };

    private static bool Has(ReportPermission effective, ReportPermission required)
        => (effective & required) == required;
}

#pragma warning restore MA0048
