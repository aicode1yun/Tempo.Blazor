using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// EF Core <see cref="IReportPermissionStore"/>. Per-folder grants are persisted as
/// <see cref="ReportFolderPermissionEntity"/> rows (subject = OIDC <c>sub</c>, role name); folder
/// inheritance is resolved against the catalog folder tree, and grants are expanded into
/// <see cref="ReportFolderAclEntry"/> allow entries the resolver already understands.
/// </summary>
public sealed class EfReportFolderPermissionStore : IReportPermissionStore
{
    private const string RoleAdmin = "Admin";
    private const string RoleAuthor = "Author";
    private const string RoleViewer = "Viewer";

    private readonly ReportServerDbContext _dbContext;

    /// <summary>Creates the store.</summary>
    public EfReportFolderPermissionStore(ReportServerDbContext dbContext)
        => _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    /// <remarks>Folder hierarchy is owned by the catalog; nothing extra to persist here.</remarks>
    public Task SaveFolderAsync(ReportFolderPermissionNode folder, ReportExecutionContext context)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Replaces the user-subject allow grants on a single folder.</remarks>
    public async Task SetAclEntriesAsync(
        string folderId,
        IReadOnlyList<ReportFolderAclEntry> entries,
        ReportExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var tenantId = context.TenantId;

        var existing = await _dbContext.FolderPermissions
            .Where(permission => permission.TenantId == tenantId && permission.FolderId == folderId)
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);
        _dbContext.FolderPermissions.RemoveRange(existing);

        var path = await ResolveFolderPathAsync(folderId, context).ConfigureAwait(false);
        foreach (var entry in entries.Where(entry =>
                     entry.SubjectKind == ReportAclSubjectKind.User && entry.Effect == ReportAclEffect.Allow))
        {
            _dbContext.FolderPermissions.Add(new ReportFolderPermissionEntity
            {
                TenantId = tenantId,
                FolderId = folderId,
                Path = path.LastOrDefault() ?? folderId,
                SubjectId = entry.SubjectId,
                Role = RoleFromPermissions(entry.Permissions),
            });
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Grants a role to a subject on a folder (upsert).</summary>
    public async Task GrantAsync(
        string folderId,
        string subjectId,
        ReportServerRole role,
        ReportExecutionContext context)
    {
        var tenantId = context.TenantId;
        var entity = await _dbContext.FolderPermissions
            .FirstOrDefaultAsync(
                permission => permission.TenantId == tenantId &&
                    permission.FolderId == folderId &&
                    permission.SubjectId == subjectId,
                context.CancellationToken)
            .ConfigureAwait(false);
        var path = await ResolveFolderPathAsync(folderId, context).ConfigureAwait(false);
        if (entity is null)
        {
            _dbContext.FolderPermissions.Add(new ReportFolderPermissionEntity
            {
                TenantId = tenantId,
                FolderId = folderId,
                Path = path.LastOrDefault() ?? folderId,
                SubjectId = subjectId,
                Role = RoleName(role),
            });
        }
        else
        {
            entity.Role = RoleName(role);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The EF store persists user-subject allow grants only (role-projected), matching the F4 folder
    /// ACL design. Role/application subjects and deny entries are not persisted here.
    /// </remarks>
    public async Task GrantAclEntryAsync(
        string folderId,
        ReportFolderAclEntry entry,
        ReportExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.SubjectKind != ReportAclSubjectKind.User || entry.Effect != ReportAclEffect.Allow)
        {
            return;
        }

        await GrantAsync(folderId, entry.SubjectId, RoleForPermissions(entry.Permissions), context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFolderAclEntry>> ListFolderAclEntriesAsync(
        string folderId,
        ReportExecutionContext context)
    {
        var tenantId = context.TenantId;
        var grants = await _dbContext.FolderPermissions
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && permission.FolderId == folderId)
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);

        return grants
            .Select(grant => ReportFolderAclEntry.AllowUser(
                grant.FolderId,
                grant.SubjectId,
                PermissionsFromRole(grant.Role)) with { TenantId = tenantId })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task RevokeAclEntryAsync(
        string folderId,
        ReportAclSubjectKind subjectKind,
        string subjectId,
        ReportExecutionContext context)
    {
        if (subjectKind != ReportAclSubjectKind.User)
        {
            return;
        }

        var tenantId = context.TenantId;
        var existing = await _dbContext.FolderPermissions
            .Where(permission => permission.TenantId == tenantId &&
                permission.FolderId == folderId &&
                permission.SubjectId == subjectId)
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (existing.Count == 0)
        {
            return;
        }

        _dbContext.FolderPermissions.RemoveRange(existing);
        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    private static ReportServerRole RoleForPermissions(ReportPermission permissions)
    {
        if (permissions.HasFlag(ReportPermission.ManagePermissions) || permissions == ReportPermission.All)
        {
            return ReportServerRole.TenantAdmin;
        }

        return permissions.HasFlag(ReportPermission.EditDefinition) ? ReportServerRole.Author : ReportServerRole.Viewer;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFolderAclEntry>> ListInheritedAclEntriesAsync(
        string? folderId,
        ReportExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return [];
        }

        var tenantId = context.TenantId;
        var path = await ResolveFolderPathAsync(folderId, context).ConfigureAwait(false);
        if (path.Count == 0)
        {
            path = [folderId];
        }

        var grants = await _dbContext.FolderPermissions
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && path.Contains(permission.FolderId))
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);

        return grants
            .Select(grant => ReportFolderAclEntry.AllowUser(
                grant.FolderId,
                grant.SubjectId,
                PermissionsFromRole(grant.Role)) with { TenantId = tenantId })
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> ResolveFolderPathAsync(string folderId, ReportExecutionContext context)
    {
        var folders = await _dbContext.Folders
            .AsNoTracking()
            .Select(folder => new { folder.FolderId, folder.ParentFolderId })
            .ToDictionaryAsync(folder => folder.FolderId, folder => folder.ParentFolderId, context.CancellationToken)
            .ConfigureAwait(false);

        var stack = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string? current = folderId;
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            stack.Push(current);
            current = folders.TryGetValue(current, out var parent) ? parent : null;
        }

        return stack.ToArray();
    }

    private static string RoleName(ReportServerRole role)
        => role switch
        {
            ReportServerRole.TenantAdmin => RoleAdmin,
            ReportServerRole.Author => RoleAuthor,
            _ => RoleViewer,
        };

    private static string RoleFromPermissions(ReportPermission permissions)
    {
        if (permissions.HasFlag(ReportPermission.ManagePermissions) || permissions == ReportPermission.All)
        {
            return RoleAdmin;
        }

        return permissions.HasFlag(ReportPermission.EditDefinition) ? RoleAuthor : RoleViewer;
    }

    private static ReportPermission PermissionsFromRole(string role)
        => role switch
        {
            RoleAdmin => ReportPermission.All,
            RoleAuthor => ReportPermission.View | ReportPermission.Render | ReportPermission.Export | ReportPermission.EditDefinition,
            _ => ReportPermission.View | ReportPermission.Render | ReportPermission.Export,
        };
}
