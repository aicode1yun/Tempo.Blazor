using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>Tenant-scoped report server store abstraction.</summary>
public interface IReportServerStore
{
    /// <summary>Gets folders for the current tenant.</summary>
    Task<IReadOnlyList<ReportFolderDto>> GetFoldersAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Creates a folder.</summary>
    Task<ReportFolderDto> CreateFolderAsync(CreateReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Updates a folder.</summary>
    Task<ReportFolderDto?> UpdateFolderAsync(string tenantId, string folderId, UpdateReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Moves a folder.</summary>
    Task<ReportFolderDto?> MoveFolderAsync(string tenantId, string folderId, MoveReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a folder.</summary>
    Task<bool> DeleteFolderAsync(string tenantId, string folderId, CancellationToken cancellationToken = default);

    /// <summary>Searches reports.</summary>
    Task<IReadOnlyList<ReportSummaryDto>> SearchReportsAsync(ReportSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets a report detail.</summary>
    Task<ReportDetailDto?> GetReportAsync(string tenantId, string reportId, CancellationToken cancellationToken = default);

    /// <summary>Creates a report with its first immutable revision.</summary>
    Task<ReportDetailDto> CreateReportAsync(CreateReportRequestDto request, string userId, CancellationToken cancellationToken = default);

    /// <summary>Moves a report.</summary>
    Task<ReportDetailDto?> MoveReportAsync(string tenantId, string reportId, MoveReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a report.</summary>
    Task<bool> DeleteReportAsync(string tenantId, string reportId, CancellationToken cancellationToken = default);

    /// <summary>Creates an immutable revision.</summary>
    Task<ReportRevisionDto?> AddRevisionAsync(UpdateReportDefinitionRequestDto request, string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets revisions for a report.</summary>
    Task<IReadOnlyList<ReportRevisionDto>> GetRevisionsAsync(string tenantId, string reportId, CancellationToken cancellationToken = default);

    /// <summary>Publishes a revision.</summary>
    Task<ReportRevisionDto?> PublishRevisionAsync(string tenantId, string reportId, PublishReportRevisionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Copies a previous revision into a new latest revision.</summary>
    Task<ReportRevisionDto?> RollbackAsync(string tenantId, string reportId, RollbackReportRevisionRequestDto request, string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets data sources for a tenant.</summary>
    Task<IReadOnlyList<ReportDataSourceDto>> GetDataSourcesAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a data source.</summary>
    Task<ReportDataSourceDto> UpsertDataSourceAsync(UpsertReportDataSourceRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets one data source.</summary>
    Task<ReportDataSourceDto?> GetDataSourceAsync(string tenantId, string dataSourceId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a data source.</summary>
    Task<bool> DeleteDataSourceAsync(string tenantId, string dataSourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-user report favorite. Owned by a tenant and scoped to a single user (the OIDC subject / actor
/// id). Not covered by the ambient tenant query filter: favorites are read through the tenant-scoped
/// store methods which constrain by <see cref="TenantId"/> and <see cref="UserId"/> explicitly.
/// </summary>
public sealed class ReportFavoriteEntity
{
    /// <summary>Surrogate identifier.</summary>
    public long Id { get; set; }

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>User identifier (the OIDC subject / actor id) that owns the favorite.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Favorited report identifier.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>When the favorite was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Immutable record of a single ad-hoc (synchronous) report render run. Persisted for both success and
/// failure so the portal can show a per-user render history that is richer than the audit trail. Not
/// covered by the ambient tenant query filter: runs are read through the tenant-scoped store methods
/// which constrain by <see cref="TenantId"/> and <see cref="ActorId"/> explicitly.
/// </summary>
public sealed class RenderRunEntity
{
    /// <summary>Surrogate identifier.</summary>
    public long Id { get; set; }

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Actor that requested the render (the OIDC subject / actor id).</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Rendered report identifier.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Serialized render parameter values.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Requested output format token (Snapshot, Pdf, Csv, Xlsx, Png).</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Outcome token mapped from the render execution
    /// (Succeeded/PageQuotaExceeded/OutputTooLarge/TimedOut/Overloaded/Failed).
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Number of rendered pages, when the render produced output.</summary>
    public int? PageCount { get; set; }

    /// <summary>Rendered payload size in bytes, when the render produced output.</summary>
    public long? ByteSize { get; set; }

    /// <summary>Wall-clock render duration in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>When the run was recorded.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Report server quota, concurrency, and timeout limits, bound from the <c>Rendering</c> section.</summary>
public sealed record ReportServerQuotaOptions
{
    /// <summary>Maximum pages allowed for synchronous renders.</summary>
    public int MaxSynchronousPages { get; init; } = 20;

    /// <summary>Maximum pages allowed for queued renders.</summary>
    public int MaxQueuedPages { get; init; } = 200;

    /// <summary>Maximum wall-clock duration for a single synchronous render before it is cancelled.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum number of renders allowed to execute concurrently across all tenants.</summary>
    public int MaxConcurrentRenders { get; init; } = 4;

    /// <summary>
    /// Maximum number of renders allowed to wait for a concurrency slot. A request that arrives when
    /// the queue is full is rejected immediately (HTTP 429) instead of piling up unbounded work.
    /// </summary>
    public int MaxRenderQueueLength { get; init; } = 50;

    /// <summary>Maximum size, in bytes, of a synchronous render payload before it is rejected (HTTP 413).</summary>
    public long MaxOutputBytes { get; init; } = 50L * 1024 * 1024;
}
