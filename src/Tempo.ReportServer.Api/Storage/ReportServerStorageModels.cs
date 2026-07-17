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
