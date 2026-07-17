using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Web.Tests.Fixtures;

/// <summary>
/// In-memory <see cref="ITempoReportServerClient"/> used by the catalog page tests. It mirrors the
/// behaviours the pages depend on (folder tree, report search, revision history + rollback, data-source
/// upsert + connection test) so the bUnit tests exercise the real cutover path against a client mock
/// rather than the retired in-memory dogfooding store.
/// </summary>
public sealed class FakeTempoReportServerClient : ITempoReportServerClient
{
    private const string Tenant = "northwind";

    private readonly List<ReportFolderDto> _folders =
    [
        new() { TenantId = Tenant, FolderId = "folder-finance", ParentFolderId = null, Name = "Finance", Path = "/Finance" },
        new() { TenantId = Tenant, FolderId = "folder-ops", ParentFolderId = null, Name = "Operations", Path = "/Operations" },
    ];

    private readonly List<ReportSummaryDto> _reports =
    [
        new()
        {
            TenantId = Tenant,
            ReportId = "sales-register",
            FolderId = "folder-finance",
            Name = "Sales Register",
            Description = "Sales orders, totals and payment status.",
            LatestRevisionId = "rev-sr-2",
            CreatedAt = DateTimeOffset.Parse("2026-06-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-06-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        },
        new()
        {
            TenantId = Tenant,
            ReportId = "fulfillment-sla",
            FolderId = "folder-ops",
            Name = "Fulfillment SLA",
            Description = "Warehouse SLA by region and carrier.",
            LatestRevisionId = "rev-fs-1",
            CreatedAt = DateTimeOffset.Parse("2026-06-05T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-06-18T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        },
    ];

    private readonly List<ReportRevisionDto> _revisions =
    [
        Revision("rev-sr-1", "sales-register", 1, "Initial revision."),
        Revision("rev-sr-2", "sales-register", 2, "Added IncludeClosed parameter."),
        Revision("rev-fs-1", "fulfillment-sla", 1, "Initial revision."),
    ];

    private readonly List<ReportDataSourceDto> _dataSources =
    [
        new() { TenantId = Tenant, DataSourceId = "ds-erp", Name = "ERP SQL", Kind = "SQL", Connection = "Server=erp-sql;Database=Reporting;" },
        new() { TenantId = Tenant, DataSourceId = "ds-crm", Name = "CRM REST", Kind = "REST JSON", Connection = "" },
    ];

    private int _idCounter;

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportFolderDto>> GetFoldersAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportFolderDto>>([.. _folders.OrderBy(folder => folder.Path, StringComparer.Ordinal)]);

    /// <inheritdoc />
    public Task<ReportFolderDto> CreateFolderAsync(CreateReportFolderRequestDto request, CancellationToken cancellationToken = default)
    {
        var parent = _folders.FirstOrDefault(folder => folder.FolderId == request.ParentFolderId);
        var path = parent is null
            ? "/" + request.Name.Trim('/')
            : parent.Path.TrimEnd('/') + "/" + request.Name.Trim('/');
        var folder = new ReportFolderDto
        {
            TenantId = request.TenantId,
            FolderId = $"folder-{++_idCounter}",
            ParentFolderId = string.IsNullOrWhiteSpace(request.ParentFolderId) ? null : request.ParentFolderId,
            Name = request.Name.Trim(),
            Path = path,
        };
        _folders.Add(folder);
        return Task.FromResult(folder);
    }

    /// <inheritdoc />
    public Task<ReportDetailDto> MoveReportAsync(string reportId, string tenantId, MoveReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var index = _reports.FindIndex(report => report.ReportId == reportId);
        if (index >= 0)
        {
            _reports[index] = _reports[index] with { FolderId = request.FolderId, UpdatedAt = DateTimeOffset.UtcNow };
        }

        return Task.FromResult(new ReportDetailDto { ReportId = reportId, TenantId = tenantId, FolderId = request.FolderId });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportSummaryDto>> SearchReportsAsync(ReportSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var reports = _reports.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.FolderId))
        {
            reports = reports.Where(report => report.FolderId == request.FolderId);
        }

        return Task.FromResult<IReadOnlyList<ReportSummaryDto>>([.. reports.OrderBy(report => report.Name, StringComparer.Ordinal)]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportRevisionDto>> GetRevisionsAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportRevisionDto>>(
            [.. _revisions.Where(revision => revision.ReportId == reportId).OrderByDescending(revision => revision.RevisionNumber)]);

    /// <inheritdoc />
    public Task<ReportRevisionDto> RollbackRevisionAsync(string reportId, string tenantId, RollbackReportRevisionRequestDto request, CancellationToken cancellationToken = default)
    {
        var source = _revisions.First(revision => revision.ReportId == reportId && revision.RevisionId == request.RevisionId);
        var nextNumber = _revisions.Where(revision => revision.ReportId == reportId).Max(revision => revision.RevisionNumber) + 1;
        var revision = Revision(
            $"rev-{++_idCounter}",
            reportId,
            nextNumber,
            request.Comment ?? $"Rollback to revision {source.RevisionNumber}");
        _revisions.Add(revision);

        var index = _reports.FindIndex(report => report.ReportId == reportId);
        if (index >= 0)
        {
            _reports[index] = _reports[index] with { LatestRevisionId = revision.RevisionId, UpdatedAt = DateTimeOffset.UtcNow };
        }

        return Task.FromResult(revision);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportDataSourceDto>> GetDataSourcesAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportDataSourceDto>>([.. _dataSources.OrderBy(source => source.Name, StringComparer.Ordinal)]);

    /// <inheritdoc />
    public Task<ReportDataSourceDto> UpsertDataSourceAsync(UpsertReportDataSourceRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = _dataSources.FindIndex(source => source.Name == request.Name);
        var source = new ReportDataSourceDto
        {
            TenantId = request.TenantId,
            DataSourceId = existing >= 0 ? _dataSources[existing].DataSourceId : $"ds-{++_idCounter}",
            Name = request.Name.Trim(),
            Kind = request.Kind.Trim(),
            Connection = request.Connection,
        };
        if (existing >= 0)
        {
            _dataSources[existing] = source;
        }
        else
        {
            _dataSources.Add(source);
        }

        return Task.FromResult(source);
    }

    /// <inheritdoc />
    public Task<ReportDataSourceConnectionTestResultDto> TestDataSourceConnectionAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default)
    {
        var source = _dataSources.First(item => item.DataSourceId == dataSourceId);
        var hasConnection = !string.IsNullOrWhiteSpace(source.Connection);
        return Task.FromResult(new ReportDataSourceConnectionTestResultDto
        {
            Success = hasConnection,
            Message = hasConnection ? "Connection metadata is valid." : "Connection is empty.",
        });
    }

    private static ReportRevisionDto Revision(string revisionId, string reportId, int number, string comment)
        => new()
        {
            TenantId = Tenant,
            RevisionId = revisionId,
            ReportId = reportId,
            RevisionNumber = number,
            CreatedByUserId = "Pavel Author",
            CreatedAt = DateTimeOffset.Parse("2026-06-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture).AddHours(number),
            Comment = comment,
            IsPublished = number == 1,
        };

    // ---- Members not exercised by the catalog pages -----------------------------------------

    public Task<ReportFolderDto> UpdateFolderAsync(string folderId, string tenantId, UpdateReportFolderRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportFolderDto> MoveFolderAsync(string folderId, string tenantId, MoveReportFolderRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteFolderAsync(string folderId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportDetailDto> GetReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportDetailDto> CreateReportAsync(CreateReportRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportRevisionDto> UpdateReportDefinitionAsync(UpdateReportDefinitionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportRevisionDto> PublishRevisionAsync(string reportId, string tenantId, PublishReportRevisionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RenderReportResultDto> RenderAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RenderJobDto> QueueRenderAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RenderJobDto> GetRenderJobAsync(string jobId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteDataSourceAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportDataSourceSchemaDto> GetDataSourceSchemaAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportDataSourcePreviewDto> PreviewDataSourceAsync(string dataSourceId, string tenantId, int top = 5, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportScheduleDto>> GetSchedulesAsync(string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportScheduleDto?> GetScheduleAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportScheduleDto> UpsertScheduleAsync(UpsertReportScheduleRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task SetScheduleEnabledAsync(string scheduleId, SetReportScheduleEnabledRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteScheduleAsync(string scheduleId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportScheduleRunDto>> GetScheduleRunsAsync(string tenantId, string scheduleId, int max = 20, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CreateReportApiKeyResultDto> CreateApiKeyAsync(CreateReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportApiKeyDto>> GetApiKeysAsync(string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CreateReportApiKeyResultDto> RotateApiKeyAsync(string keyId, RotateReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task RevokeApiKeyAsync(string keyId, RevokeReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportAuditEventDto>> QueryAuditAsync(
        string tenantId,
        ReportAuditActionDto? action = null,
        ReportAuditOutcomeDto? outcome = null,
        string? actorId = null,
        string? resourceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? take = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportFolderAclEntryDto> GrantPermissionAsync(GrantReportPermissionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportFolderAclEntryDto>> GetFolderPermissionsAsync(string tenantId, string folderId, string? subjectId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task RevokePermissionAsync(RevokeReportPermissionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportResolveResultDto> ResolveReportAsync(string tenantId, string? reportId = null, string? path = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
