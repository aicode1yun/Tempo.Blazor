#pragma warning disable MA0048, MA0158

using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Security;

/// <summary>Tenant-scoped permission store.</summary>
public interface IReportPermissionStore
{
    /// <summary>Saves a folder node for ACL inheritance.</summary>
    Task SaveFolderAsync(ReportFolderPermissionNode folder, ReportExecutionContext context);

    /// <summary>Replaces ACL entries for a folder.</summary>
    Task SetAclEntriesAsync(
        string folderId,
        IReadOnlyList<ReportFolderAclEntry> entries,
        ReportExecutionContext context);

    /// <summary>Loads ACL entries from root to the requested folder.</summary>
    Task<IReadOnlyList<ReportFolderAclEntry>> ListInheritedAclEntriesAsync(
        string? folderId,
        ReportExecutionContext context);

    /// <summary>Grants (or replaces) a single ACL entry for a subject on a folder.</summary>
    Task GrantAclEntryAsync(
        string folderId,
        ReportFolderAclEntry entry,
        ReportExecutionContext context);

    /// <summary>Lists the ACL entries defined directly on a folder (not inherited from ancestors).</summary>
    Task<IReadOnlyList<ReportFolderAclEntry>> ListFolderAclEntriesAsync(
        string folderId,
        ReportExecutionContext context);

    /// <summary>Revokes a subject's ACL grant on a folder.</summary>
    Task RevokeAclEntryAsync(
        string folderId,
        ReportAclSubjectKind subjectKind,
        string subjectId,
        ReportExecutionContext context);
}

/// <summary>In-memory permission store for tests and single-node development.</summary>
public sealed class InMemoryReportPermissionStore : IReportPermissionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TenantPermissions> _tenants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SaveFolderAsync(ReportFolderPermissionNode folder, ReportExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(folder);
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            state.Folders[folder.FolderId] = folder with { TenantId = context.TenantId };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetAclEntriesAsync(
        string folderId,
        IReadOnlyList<ReportFolderAclEntry> entries,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            state.Acls[folderId] = entries
                .Select(entry => entry with { TenantId = context.TenantId, FolderId = folderId })
                .ToArray();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportFolderAclEntry>> ListInheritedAclEntriesAsync(
        string? folderId,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            var path = ResolveFolderPath(state, folderId);
            var entries = path
                .SelectMany(id => state.Acls.TryGetValue(id, out var acl) ? acl : [])
                .ToArray();
            return Task.FromResult((IReadOnlyList<ReportFolderAclEntry>)entries);
        }
    }

    /// <inheritdoc />
    public Task GrantAclEntryAsync(
        string folderId,
        ReportFolderAclEntry entry,
        ReportExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            var normalized = entry with { TenantId = context.TenantId, FolderId = folderId };
            var current = state.Acls.TryGetValue(folderId, out var existing)
                ? existing.ToList()
                : [];
            current.RemoveAll(candidate =>
                candidate.SubjectKind == normalized.SubjectKind &&
                string.Equals(candidate.SubjectId, normalized.SubjectId, StringComparison.Ordinal) &&
                candidate.Effect == normalized.Effect);
            current.Add(normalized);
            state.Acls[folderId] = current.ToArray();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportFolderAclEntry>> ListFolderAclEntriesAsync(
        string folderId,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            var entries = state.Acls.TryGetValue(folderId, out var existing)
                ? existing.ToArray()
                : [];
            return Task.FromResult((IReadOnlyList<ReportFolderAclEntry>)entries);
        }
    }

    /// <inheritdoc />
    public Task RevokeAclEntryAsync(
        string folderId,
        ReportAclSubjectKind subjectKind,
        string subjectId,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            if (state.Acls.TryGetValue(folderId, out var existing))
            {
                state.Acls[folderId] = existing
                    .Where(entry => !(entry.SubjectKind == subjectKind &&
                        string.Equals(entry.SubjectId, subjectId, StringComparison.Ordinal)))
                    .ToArray();
            }
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> ResolveFolderPath(TenantPermissions state, string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return [];
        }

        var path = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = folderId;
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            path.Push(current);
            current = state.Folders.TryGetValue(current, out var folder)
                ? folder.ParentFolderId
                : null;
        }

        return path.ToArray();
    }

    private TenantPermissions State(string tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var state))
        {
            state = new TenantPermissions();
            _tenants[tenantId] = state;
        }

        return state;
    }

    private sealed class TenantPermissions
    {
        public Dictionary<string, ReportFolderPermissionNode> Folders { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<ReportFolderAclEntry>> Acls { get; } = new(StringComparer.Ordinal);
    }
}

#pragma warning restore MA0048, MA0158
