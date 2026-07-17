#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Dtos;

/// <summary>Folder DTO used by report server APIs.</summary>
public sealed record ReportFolderDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Optional parent folder identifier.</summary>
    public string? ParentFolderId { get; init; }

    /// <summary>Folder name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Canonical folder path.</summary>
    public string Path { get; init; } = string.Empty;
}

/// <summary>Report catalog summary DTO.</summary>
public sealed record ReportSummaryDto
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

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Report detail DTO.</summary>
public sealed record ReportDetailDto
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

    /// <summary>Canonical report definition JSON.</summary>
    public string DefinitionJson { get; init; } = string.Empty;
}

/// <summary>Report revision DTO.</summary>
public sealed record ReportRevisionDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Revision identifier.</summary>
    public string RevisionId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Monotonic revision number.</summary>
    public int RevisionNumber { get; init; }

    /// <summary>Canonical report definition JSON.</summary>
    public string DefinitionJson { get; init; } = string.Empty;

    /// <summary>Revision author user identifier.</summary>
    public string CreatedByUserId { get; init; } = string.Empty;

    /// <summary>Revision creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Optional revision note.</summary>
    public string? Comment { get; init; }

    /// <summary>Whether this revision is published.</summary>
    public bool IsPublished { get; init; }
}

/// <summary>Request for creating a report.</summary>
public sealed record CreateReportRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Target folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;

    /// <summary>Report name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Canonical report definition JSON.</summary>
    public string DefinitionJson { get; init; } = string.Empty;
}

/// <summary>Request for creating a report folder.</summary>
public sealed record CreateReportFolderRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Optional parent folder identifier.</summary>
    public string? ParentFolderId { get; init; }

    /// <summary>Folder name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Request for updating a report folder.</summary>
public sealed record UpdateReportFolderRequestDto
{
    /// <summary>Folder name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Request for moving a folder to another parent.</summary>
public sealed record MoveReportFolderRequestDto
{
    /// <summary>New parent folder identifier, or null for the root.</summary>
    public string? ParentFolderId { get; init; }
}

/// <summary>Report catalog search request.</summary>
public sealed record ReportSearchRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Optional folder scope.</summary>
    public string? FolderId { get; init; }

    /// <summary>Optional free-text query.</summary>
    public string? Query { get; init; }
}

/// <summary>Request for moving a report to another folder.</summary>
public sealed record MoveReportRequestDto
{
    /// <summary>Target folder identifier.</summary>
    public string FolderId { get; init; } = string.Empty;
}

/// <summary>Request for publishing a revision.</summary>
public sealed record PublishReportRevisionRequestDto
{
    /// <summary>Revision identifier.</summary>
    public string RevisionId { get; init; } = string.Empty;
}

/// <summary>Request for rolling back a report to a previous revision.</summary>
public sealed record RollbackReportRevisionRequestDto
{
    /// <summary>Revision identifier to copy into a new latest revision.</summary>
    public string RevisionId { get; init; } = string.Empty;

    /// <summary>Optional rollback note.</summary>
    public string? Comment { get; init; }
}

/// <summary>Request for updating a report definition and creating a new revision.</summary>
public sealed record UpdateReportDefinitionRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Expected current revision for optimistic concurrency.</summary>
    public string? ExpectedRevisionId { get; init; }

    /// <summary>Canonical report definition JSON.</summary>
    public string DefinitionJson { get; init; } = string.Empty;

    /// <summary>Optional revision note.</summary>
    public string? Comment { get; init; }
}

/// <summary>Supported render output formats.</summary>
public enum ReportRenderFormat
{
    /// <summary>Viewer snapshot JSON.</summary>
    Snapshot,

    /// <summary>PDF document.</summary>
    Pdf,

    /// <summary>Excel workbook.</summary>
    Xlsx,

    /// <summary>CSV export.</summary>
    Csv,

    /// <summary>PNG raster image.</summary>
    Png,
}

/// <summary>Parameter value DTO used by render requests.</summary>
public sealed record ReportParameterValueDto
{
    /// <summary>Parameter name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Serialized parameter values.</summary>
    public List<string> Values { get; init; } = [];
}

/// <summary>Request for rendering a report.</summary>
public sealed record RenderReportRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Optional revision identifier.</summary>
    public string? RevisionId { get; init; }

    /// <summary>Requested output format.</summary>
    public ReportRenderFormat Format { get; init; } = ReportRenderFormat.Snapshot;

    /// <summary>Culture name used for rendering.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Parameter values.</summary>
    public List<ReportParameterValueDto> Parameters { get; init; } = [];
}

/// <summary>Render job status.</summary>
public enum RenderJobStatus
{
    /// <summary>Job is queued.</summary>
    Queued,

    /// <summary>Job is running.</summary>
    Running,

    /// <summary>Job completed successfully.</summary>
    Completed,

    /// <summary>Job failed.</summary>
    Failed,

    /// <summary>Job was canceled.</summary>
    Canceled,
}

/// <summary>Render job DTO.</summary>
public sealed record RenderJobDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Render job identifier.</summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Requested output format.</summary>
    public ReportRenderFormat Format { get; init; } = ReportRenderFormat.Snapshot;

    /// <summary>Current job status.</summary>
    public RenderJobStatus Status { get; init; } = RenderJobStatus.Queued;

    /// <summary>Queue timestamp.</summary>
    public DateTimeOffset QueuedAt { get; init; }

    /// <summary>Start timestamp.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Completion timestamp.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Failure message, if any.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Snapshot result URL, if available.</summary>
    public string? SnapshotUrl { get; init; }

    /// <summary>Download result URL, if available.</summary>
    public string? DownloadUrl { get; init; }
}

/// <summary>Supported report parameter metadata kind.</summary>
public enum ReportParameterMetadataKind
{
    /// <summary>String parameter.</summary>
    String,

    /// <summary>Number parameter.</summary>
    Number,

    /// <summary>Date or date-time parameter.</summary>
    Date,

    /// <summary>Boolean parameter.</summary>
    Boolean,

    /// <summary>Single choice parameter.</summary>
    Select,

    /// <summary>Multiple choice parameter.</summary>
    MultiSelect,
}

/// <summary>Parameter option metadata used by report server clients.</summary>
public sealed record ReportParameterOptionDto
{
    /// <summary>Option value serialized as text.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>User-facing option label.</summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>Report parameter metadata used by viewer parameter panels.</summary>
public sealed record ReportParameterMetadataDto
{
    /// <summary>Parameter name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>User-facing label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Parameter value kind.</summary>
    public ReportParameterMetadataKind Kind { get; init; } = ReportParameterMetadataKind.String;

    /// <summary>Whether a value is required.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Whether multiple values are accepted.</summary>
    public bool AllowMultiple { get; init; }

    /// <summary>Default serialized values.</summary>
    public List<string> DefaultValues { get; init; } = [];

    /// <summary>Available options for select parameters.</summary>
    public List<ReportParameterOptionDto> Options { get; init; } = [];
}

/// <summary>Report render response for synchronous renders and completed jobs.</summary>
public sealed record RenderReportResultDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Requested output format.</summary>
    public ReportRenderFormat Format { get; init; } = ReportRenderFormat.Snapshot;

    /// <summary>Media type of the result.</summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>Suggested file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Binary result payload. Snapshot renders contain UTF-8 JSON bytes.</summary>
    public byte[] Bytes { get; init; } = [];

    /// <summary>Snapshot JSON when the requested format is Snapshot.</summary>
    public string? SnapshotJson { get; init; }

    /// <summary>Number of rendered pages.</summary>
    public int PageCount { get; init; }
}

/// <summary>Host version metadata returned by the anonymous <c>GET /version</c> endpoint.</summary>
public sealed record ReportServerVersionDto
{
    /// <summary>Informational (or assembly) version of the running host.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Simple assembly version (major.minor.build.revision).</summary>
    public string AssemblyVersion { get; init; } = string.Empty;
}

/// <summary>Named report data source DTO.</summary>
public sealed record ReportDataSourceDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Data source identifier.</summary>
    public string DataSourceId { get; init; } = string.Empty;

    /// <summary>Unique data source name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Data source kind, such as restJson or sql.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Provider connection string or base URL. Secrets should be supplied by the server in production.</summary>
    public string Connection { get; init; } = string.Empty;
}

/// <summary>Request for creating or updating a named data source.</summary>
public sealed record UpsertReportDataSourceRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Unique data source name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Data source kind, such as restJson or sql.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Provider connection string or base URL.</summary>
    public string Connection { get; init; } = string.Empty;
}

/// <summary>Result of testing a data source connection.</summary>
public sealed record ReportDataSourceConnectionTestResultDto
{
    /// <summary>Whether the connection succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Optional diagnostic message.</summary>
    public string? Message { get; init; }
}

/// <summary>Data source schema discovery response.</summary>
public sealed record ReportDataSourceSchemaDto
{
    /// <summary>Columns discovered for the data source.</summary>
    public List<ReportDataSourceSchemaColumnDto> Columns { get; init; } = [];
}

/// <summary>Single data source schema column.</summary>
public sealed record ReportDataSourceSchemaColumnDto
{
    /// <summary>Column name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Column type name.</summary>
    public string DataType { get; init; } = string.Empty;
}

/// <summary>Data source preview response.</summary>
public sealed record ReportDataSourcePreviewDto
{
    /// <summary>Preview rows keyed by column name.</summary>
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
}

/// <summary>Provides an optional bearer token for the typed report server client.</summary>
public interface IReportServerTokenProvider
{
    /// <summary>Gets the current bearer token, or null when no bearer token should be sent.</summary>
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Typed HTTP client for Tempo Report Server endpoints.</summary>
public interface ITempoReportServerClient
{
    /// <summary>Gets tenant folders.</summary>
    Task<IReadOnlyList<ReportFolderDto>> GetFoldersAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Creates a folder.</summary>
    Task<ReportFolderDto> CreateFolderAsync(CreateReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Updates a folder.</summary>
    Task<ReportFolderDto> UpdateFolderAsync(string folderId, string tenantId, UpdateReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Moves a folder.</summary>
    Task<ReportFolderDto> MoveFolderAsync(string folderId, string tenantId, MoveReportFolderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a folder.</summary>
    Task DeleteFolderAsync(string folderId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Searches reports.</summary>
    Task<IReadOnlyList<ReportSummaryDto>> SearchReportsAsync(ReportSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets a report detail.</summary>
    Task<ReportDetailDto> GetReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Creates a report.</summary>
    Task<ReportDetailDto> CreateReportAsync(CreateReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Moves a report.</summary>
    Task<ReportDetailDto> MoveReportAsync(string reportId, string tenantId, MoveReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a report.</summary>
    Task DeleteReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Updates a report definition and creates a new revision.</summary>
    Task<ReportRevisionDto> UpdateReportDefinitionAsync(UpdateReportDefinitionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets report revisions.</summary>
    Task<IReadOnlyList<ReportRevisionDto>> GetRevisionsAsync(string reportId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Publishes a revision.</summary>
    Task<ReportRevisionDto> PublishRevisionAsync(string reportId, string tenantId, PublishReportRevisionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Rolls a report back to a previous revision by creating a new latest revision.</summary>
    Task<ReportRevisionDto> RollbackRevisionAsync(string reportId, string tenantId, RollbackReportRevisionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets report parameter metadata.</summary>
    Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(string reportId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Renders a report synchronously.</summary>
    Task<RenderReportResultDto> RenderAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Queues an asynchronous render job.</summary>
    Task<RenderJobDto> QueueRenderAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets render job status.</summary>
    Task<RenderJobDto> GetRenderJobAsync(string jobId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets data sources.</summary>
    Task<IReadOnlyList<ReportDataSourceDto>> GetDataSourcesAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a data source.</summary>
    Task<ReportDataSourceDto> UpsertDataSourceAsync(UpsertReportDataSourceRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a data source.</summary>
    Task DeleteDataSourceAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tests a data source connection.</summary>
    Task<ReportDataSourceConnectionTestResultDto> TestDataSourceConnectionAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets a data source schema.</summary>
    Task<ReportDataSourceSchemaDto> GetDataSourceSchemaAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets a top-N data source preview.</summary>
    Task<ReportDataSourcePreviewDto> PreviewDataSourceAsync(string dataSourceId, string tenantId, int top = 5, CancellationToken cancellationToken = default);
}

#pragma warning restore MA0016, MA0048
