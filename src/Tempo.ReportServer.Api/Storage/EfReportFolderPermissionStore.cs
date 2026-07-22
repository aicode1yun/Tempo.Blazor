using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// EF Core <see cref="IReportPermissionStore"/>. Per-folder grants are persisted as
/// <see cref="ReportFolderPermissionEntity"/> rows carrying the subject kind (User/Role/Application),
/// the effect (Allow/Deny) and explicit permission flags; folder inheritance is resolved against the
/// catalog folder tree and grants are reconstructed into <see cref="ReportFolderAclEntry"/> values the
/// resolver understands. Legacy rows without explicit permission bits project their role name into
/// permissions for backward compatibility.
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
    /// <remarks>Replaces every ACL entry on a single folder with the supplied set.</remarks>
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
        var pathLeaf = path.LastOrDefault() ?? folderId;
        foreach (var entry in entries)
        {
            _dbContext.FolderPermissions.Add(ToEntity(tenantId, folderId, pathLeaf, entry));
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Grants a role to a subject on a folder (legacy role-based upsert).</summary>
    /// <remarks>
    /// Stores the role name without explicit permission bits, so the row projects the role into
    /// permissions when read back. Retained for the Fáze 4 JIT-provisioning surface.
    /// </remarks>
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
                    permission.SubjectKind == (int)ReportAclSubjectKind.User &&
                    permission.SubjectId == subjectId &&
                    permission.Effect == (int)ReportAclEffect.Allow,
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
                SubjectKind = (int)ReportAclSubjectKind.User,
                Effect = (int)ReportAclEffect.Allow,
                Permissions = null,
                Role = RoleName(role),
            });
        }
        else
        {
            entity.Role = RoleName(role);
            entity.Permissions = null;
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>Upserts a single ACL entry, keyed by (folder, subject kind, subject id, effect).</remarks>
    public async Task GrantAclEntryAsync(
        string folderId,
        ReportFolderAclEntry entry,
        ReportExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var tenantId = context.TenantId;
        var subjectKind = (int)entry.SubjectKind;
        var effect = (int)entry.Effect;
        var existing = await _dbContext.FolderPermissions
            .FirstOrDefaultAsync(
                permission => permission.TenantId == tenantId &&
                    permission.FolderId == folderId &&
                    permission.SubjectKind == subjectKind &&
                    permission.SubjectId == entry.SubjectId &&
                    permission.Effect == effect,
                context.CancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            var path = await ResolveFolderPathAsync(folderId, context).ConfigureAwait(false);
            _dbContext.FolderPermissions.Add(ToEntity(tenantId, folderId, path.LastOrDefault() ?? folderId, entry));
        }
        else
        {
            existing.Permissions = (int)entry.Permissions;
            existing.Role = RoleFromPermissions(entry.Permissions);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
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

        return grants.Select(grant => ToEntry(tenantId, grant)).ToArray();
    }

    /// <inheritdoc />
    public async Task RevokeAclEntryAsync(
        string folderId,
        ReportAclSubjectKind subjectKind,
        string subjectId,
        ReportExecutionContext context)
    {
        var tenantId = context.TenantId;
        var kind = (int)subjectKind;
        var existing = await _dbContext.FolderPermissions
            .Where(permission => permission.TenantId == tenantId &&
                permission.FolderId == folderId &&
                permission.SubjectKind == kind &&
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

        return grants.Select(grant => ToEntry(tenantId, grant)).ToArray();
    }

    private static ReportFolderPermissionEntity ToEntity(
        string tenantId,
        string folderId,
        string pathLeaf,
        ReportFolderAclEntry entry)
        => new()
        {
            TenantId = tenantId,
            FolderId = folderId,
            Path = pathLeaf,
            SubjectId = entry.SubjectId,
            SubjectKind = (int)entry.SubjectKind,
            Effect = (int)entry.Effect,
            Permissions = (int)entry.Permissions,
            Role = RoleFromPermissions(entry.Permissions),
        };

    private static ReportFolderAclEntry ToEntry(string tenantId, ReportFolderPermissionEntity grant)
        => new()
        {
            TenantId = tenantId,
            FolderId = grant.FolderId,
            SubjectKind = (ReportAclSubjectKind)grant.SubjectKind,
            SubjectId = grant.SubjectId,
            Effect = (ReportAclEffect)grant.Effect,
            Permissions = grant.Permissions is { } bits
                ? (ReportPermission)bits
                : PermissionsFromRole(grant.Role),
        };

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
