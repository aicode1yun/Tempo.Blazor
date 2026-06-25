#pragma warning disable MA0016, MA0048, MA0158

namespace Tempo.Reporting.Abstractions.Data;

/// <summary>Stored report folder record.</summary>
public sealed record ReportFolderRecord
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Parent folder identifier.</summary>
    public string? ParentFolderId { get; init; }

    /// <summary>Folder name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Canonical folder path.</summary>
    public string Path { get; init; } = string.Empty;
}

/// <summary>Stored report metadata record.</summary>
public sealed record ReportDefinitionRecord
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Report name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Report description.</summary>
    public string? Description { get; init; }

    /// <summary>Latest revision identifier.</summary>
    public string? LatestRevisionId { get; init; }
}

/// <summary>Stored report definition revision record.</summary>
public sealed record ReportDefinitionRevisionRecord
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Revision identifier.</summary>
    public string RevisionId { get; init; } = string.Empty;

    /// <summary>Monotonic revision number.</summary>
    public int RevisionNumber { get; init; }

    /// <summary>Canonical report definition JSON.</summary>
    public string DefinitionJson { get; init; } = string.Empty;

    /// <summary>User that created the revision.</summary>
    public string CreatedByUserId { get; init; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Whether the revision is published.</summary>
    public bool IsPublished { get; init; }
}

/// <summary>Tenant-scoped report definition store contract.</summary>
public interface IReportDefinitionStore
{
    /// <summary>Saves or updates a folder.</summary>
    Task<ReportFolderRecord> SaveFolderAsync(ReportFolderRecord folder, ReportExecutionContext context);

    /// <summary>Lists folders for the current tenant.</summary>
    Task<IReadOnlyList<ReportFolderRecord>> ListFoldersAsync(ReportExecutionContext context);

    /// <summary>Saves a report metadata record and creates a new revision.</summary>
    Task<ReportDefinitionRevisionRecord> SaveReportAsync(
        ReportDefinitionRecord report,
        string definitionJson,
        bool publish,
        ReportExecutionContext context);

    /// <summary>Loads report metadata by id for the current tenant.</summary>
    Task<ReportDefinitionRecord?> LoadReportAsync(string reportId, ReportExecutionContext context);

    /// <summary>Lists reports in a folder for the current tenant.</summary>
    Task<IReadOnlyList<ReportDefinitionRecord>> ListReportsAsync(string folderId, ReportExecutionContext context);

    /// <summary>Lists revisions for a report in the current tenant.</summary>
    Task<IReadOnlyList<ReportDefinitionRevisionRecord>> ListRevisionsAsync(string reportId, ReportExecutionContext context);

    /// <summary>Loads one revision for a report in the current tenant.</summary>
    Task<ReportDefinitionRevisionRecord?> LoadRevisionAsync(
        string reportId,
        string revisionId,
        ReportExecutionContext context);
}

/// <summary>In-memory tenant-scoped report definition store for tests and embedded hosts.</summary>
public sealed class InMemoryReportDefinitionStore : IReportDefinitionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TenantState> _tenants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ReportFolderRecord> SaveFolderAsync(ReportFolderRecord folder, ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            var stored = folder with { TenantId = context.TenantId };
            state.Folders[stored.FolderId] = stored;
            return Task.FromResult(stored);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportFolderRecord>> ListFoldersAsync(ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult((IReadOnlyList<ReportFolderRecord>)State(context.TenantId).Folders.Values
                .OrderBy(folder => folder.Path, StringComparer.Ordinal)
                .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<ReportDefinitionRevisionRecord> SaveReportAsync(
        ReportDefinitionRecord report,
        string definitionJson,
        bool publish,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var state = State(context.TenantId);
            var existingRevisions = state.Revisions.TryGetValue(report.ReportId, out var list)
                ? list
                : state.Revisions[report.ReportId] = [];
            var nextNumber = existingRevisions.Count + 1;
            var revision = new ReportDefinitionRevisionRecord
            {
                TenantId = context.TenantId,
                ReportId = report.ReportId,
                RevisionId = $"{report.ReportId}-r{nextNumber}",
                RevisionNumber = nextNumber,
                DefinitionJson = definitionJson,
                CreatedByUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                IsPublished = publish,
            };
            existingRevisions.Add(revision);
            state.Reports[report.ReportId] = report with
            {
                TenantId = context.TenantId,
                LatestRevisionId = revision.RevisionId,
            };
            return Task.FromResult(revision);
        }
    }

    /// <inheritdoc />
    public Task<ReportDefinitionRecord?> LoadReportAsync(string reportId, ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            State(context.TenantId).Reports.TryGetValue(reportId, out var report);
            return Task.FromResult(report);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportDefinitionRecord>> ListReportsAsync(string folderId, ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult((IReadOnlyList<ReportDefinitionRecord>)State(context.TenantId).Reports.Values
                .Where(report => string.Equals(report.FolderId, folderId, StringComparison.Ordinal))
                .OrderBy(report => report.Name, StringComparer.Ordinal)
                .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportDefinitionRevisionRecord>> ListRevisionsAsync(
        string reportId,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var revisions = State(context.TenantId).Revisions.TryGetValue(reportId, out var list)
                ? list.OrderBy(revision => revision.RevisionNumber).ToArray()
                : [];
            return Task.FromResult((IReadOnlyList<ReportDefinitionRevisionRecord>)revisions);
        }
    }

    /// <inheritdoc />
    public Task<ReportDefinitionRevisionRecord?> LoadRevisionAsync(
        string reportId,
        string revisionId,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var revision = State(context.TenantId).Revisions.TryGetValue(reportId, out var list)
                ? list.FirstOrDefault(item => string.Equals(item.RevisionId, revisionId, StringComparison.Ordinal))
                : null;
            return Task.FromResult(revision);
        }
    }

    private TenantState State(string tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var state))
        {
            state = new TenantState();
            _tenants[tenantId] = state;
        }

        return state;
    }

    private sealed class TenantState
    {
        public Dictionary<string, ReportFolderRecord> Folders { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ReportDefinitionRecord> Reports { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<ReportDefinitionRevisionRecord>> Revisions { get; } = new(StringComparer.Ordinal);
    }
}

#pragma warning restore MA0016, MA0048, MA0158
