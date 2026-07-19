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

    // The demo store seeds a deterministic, active embedding key so the API-keys page has a stable
    // row to rotate/revoke against (mirrors the retired DemoReportApiKeyStore seed).
    private readonly List<ReportApiKeyDto> _apiKeys =
    [
        new()
        {
            KeyId = "rk_demo_embed",
            TenantId = Tenant,
            ApplicationId = "embedded-app",
            Permissions = ReportPermissionsDto.View | ReportPermissionsDto.Render,
            CreatedAt = DateTimeOffset.Parse("2026-06-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            IsActive = true,
        },
    ];

    private readonly List<ReportAuditEventDto> _audit = [];

    private readonly List<ReportFolderAclEntryDto> _acls =
    [
        new()
        {
            TenantId = Tenant,
            FolderId = "folder-finance",
            SubjectKind = ReportAclSubjectKindDto.Role,
            SubjectId = "finance-admins",
            Effect = ReportAclEffectDto.Allow,
            Permissions = ReportPermissionsDto.All,
        },
    ];

    private readonly List<ReportScheduleDto> _schedules =
    [
        new()
        {
            TenantId = Tenant,
            ScheduleId = "weekly-sales",
            OwnerUserId = "Pavel Author",
            Name = "Weekly sales digest",
            ReportId = "sales-register",
            CronExpression = "0 8 * * 1",
            Format = ReportScheduleFormat.Pdf,
            DeliveryKind = ReportScheduleDeliveryKind.Email,
            DeliveryTarget = "finance@example.test",
            IsEnabled = true,
            NextRunUtc = DateTimeOffset.Parse("2026-07-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            LastDeliveredUtc = DateTimeOffset.Parse("2026-07-13T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            LastStatus = ReportScheduleRunStatus.Delivered,
            LastStatusMessage = "Delivered sales-register.pdf",
        },
    ];

    private readonly List<ReportScheduleRunDto> _runs =
    [
        new()
        {
            RunId = "run-weekly-1",
            TenantId = Tenant,
            ScheduleId = "weekly-sales",
            OccurrenceUtc = DateTimeOffset.Parse("2026-07-13T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            StartedUtc = DateTimeOffset.Parse("2026-07-13T08:00:01Z", System.Globalization.CultureInfo.InvariantCulture),
            CompletedUtc = DateTimeOffset.Parse("2026-07-13T08:00:04Z", System.Globalization.CultureInfo.InvariantCulture),
            Status = ReportScheduleRunStatus.Delivered,
            Attempt = 1,
            DeliveryKind = ReportScheduleDeliveryKind.Email,
            DeliveryTarget = "finance@example.test",
            ArtifactFileName = "sales-register.pdf",
            ArtifactContentType = "application/pdf",
            ArtifactByteCount = 2048,
        },
    ];

    // Fáze 12 pass 2 in-memory state + call capture so the portal UI tests exercise a real client path.
    private readonly List<ReportFavoriteDto> _favorites = [];
    private readonly List<RenderRunDto> _renderRuns = [];

    /// <summary>Report ids passed to <see cref="AddFavoriteAsync"/>, in call order.</summary>
    public List<string> AddedFavoriteReportIds { get; } = [];

    /// <summary>Report ids passed to <see cref="RemoveFavoriteAsync"/>, in call order.</summary>
    public List<string> RemovedFavoriteReportIds { get; } = [];

    /// <summary>Render requests passed to <see cref="RenderAsync"/>, in call order.</summary>
    public List<RenderReportRequestDto> RenderRequests { get; } = [];

    /// <summary>The most recent request captured by <see cref="CreateReportAsync"/>.</summary>
    public CreateReportRequestDto? LastCreateReportRequest { get; private set; }

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
    {
        LastCreateReportRequest = request;
        var reportId = $"{Slug(request.Name)}-{++_idCounter}";
        _reports.Add(new ReportSummaryDto
        {
            TenantId = request.TenantId,
            ReportId = reportId,
            FolderId = request.FolderId,
            Name = request.Name.Trim(),
            Description = request.Description,
            LatestRevisionId = $"rev-{reportId}-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        return Task.FromResult(new ReportDetailDto
        {
            TenantId = request.TenantId,
            ReportId = reportId,
            FolderId = request.FolderId,
            Name = request.Name.Trim(),
            Description = request.Description,
            LatestRevisionId = $"rev-{reportId}-1",
            DefinitionJson = request.DefinitionJson,
        });
    }

    public Task DeleteReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportRevisionDto> UpdateReportDefinitionAsync(UpdateReportDefinitionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ReportRevisionDto> PublishRevisionAsync(string reportId, string tenantId, PublishReportRevisionRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportParameterMetadataDto>>(
        [
            new ReportParameterMetadataDto
            {
                Name = "region",
                Label = "Region",
                Kind = ReportParameterMetadataKind.String,
                IsRequired = false,
                DefaultValues = ["All"],
            },
            new ReportParameterMetadataDto
            {
                Name = "period",
                Label = "Period",
                Kind = ReportParameterMetadataKind.Select,
                IsRequired = true,
                DefaultValues = ["Q1"],
                Options =
                [
                    new ReportParameterOptionDto { Value = "Q1", Label = "Quarter 1" },
                    new ReportParameterOptionDto { Value = "Q2", Label = "Quarter 2" },
                ],
            },
        ]);

    public Task<RenderReportResultDto> RenderAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default)
    {
        // Mirror the API /render contract: rendering also PERSISTS a portal-facing render-run history
        // record, so ListRenderRunsAsync surfaces it afterwards (the same call path the real host uses).
        RenderRequests.Add(request);
        var result = new RenderReportResultDto
        {
            TenantId = request.TenantId,
            ReportId = request.ReportId,
            Format = request.Format,
            ContentType = "application/json",
            FileName = $"{request.ReportId}.{request.Format.ToString().ToLowerInvariant()}",
            Bytes = [1, 2, 3, 4],
            SnapshotJson = request.Format == ReportRenderFormat.Snapshot ? "{}" : null,
            PageCount = 1,
        };
        _renderRuns.Insert(0, new RenderRunDto
        {
            TenantId = request.TenantId,
            ActorId = "Pavel Author",
            ReportId = request.ReportId,
            Format = request.Format.ToString(),
            Outcome = "Succeeded",
            PageCount = result.PageCount,
            ByteSize = result.Bytes.LongLength,
            DurationMs = 12,
            CreatedAt = DateTimeOffset.UtcNow,
            ParametersJson = "{}",
        });
        return Task.FromResult(result);
    }

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
        => Task.FromResult<IReadOnlyList<ReportScheduleDto>>(
            [.. _schedules.Where(schedule => schedule.TenantId == tenantId).OrderBy(schedule => schedule.Name, StringComparer.Ordinal)]);

    public Task<ReportScheduleDto?> GetScheduleAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_schedules.FirstOrDefault(schedule => schedule.TenantId == tenantId && schedule.ScheduleId == scheduleId));

    public Task<ReportScheduleDto> UpsertScheduleAsync(UpsertReportScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var scheduleId = string.IsNullOrWhiteSpace(request.ScheduleId) ? Slug(request.Name) : request.ScheduleId;
        var schedule = new ReportScheduleDto
        {
            TenantId = request.TenantId,
            ScheduleId = scheduleId,
            OwnerUserId = request.OwnerUserId,
            Name = request.Name,
            ReportId = request.ReportId,
            CronExpression = request.CronExpression,
            Format = request.Format,
            CultureName = request.CultureName,
            Parameters = request.Parameters,
            DeliveryKind = request.DeliveryKind,
            DeliveryTarget = request.DeliveryTarget,
            MissedRunPolicy = request.MissedRunPolicy,
            MaxAttempts = request.MaxAttempts,
            IsEnabled = request.IsEnabled,
            NextRunUtc = DateTimeOffset.Parse("2026-07-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            LastStatus = ReportScheduleRunStatus.NeverRun,
            LastStatusMessage = "Never run",
        };
        var index = _schedules.FindIndex(item => item.TenantId == request.TenantId && item.ScheduleId == scheduleId);
        if (index >= 0)
        {
            _schedules[index] = schedule;
        }
        else
        {
            _schedules.Add(schedule);
        }

        return Task.FromResult(schedule);
    }

    public Task SetScheduleEnabledAsync(string scheduleId, SetReportScheduleEnabledRequestDto request, CancellationToken cancellationToken = default)
    {
        var index = _schedules.FindIndex(item => item.TenantId == request.TenantId && item.ScheduleId == scheduleId);
        if (index >= 0)
        {
            _schedules[index] = _schedules[index] with { IsEnabled = request.IsEnabled };
        }

        return Task.CompletedTask;
    }

    public Task DeleteScheduleAsync(string scheduleId, string tenantId, CancellationToken cancellationToken = default)
    {
        _schedules.RemoveAll(item => item.TenantId == tenantId && item.ScheduleId == scheduleId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReportScheduleRunDto>> GetScheduleRunsAsync(string tenantId, string scheduleId, int max = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportScheduleRunDto>>(
            [.. _runs.Where(run => run.TenantId == tenantId && run.ScheduleId == scheduleId).OrderByDescending(run => run.StartedUtc).Take(max)]);

    public Task<CreateReportApiKeyResultDto> CreateApiKeyAsync(CreateReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        var keyId = $"rk_{++_idCounter}";
        var key = new ReportApiKeyDto
        {
            KeyId = keyId,
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            Permissions = request.Permissions,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
        };
        _apiKeys.Add(key);
        RecordKeyAudit(request.TenantId, keyId, "create-api-key");
        return Task.FromResult(new CreateReportApiKeyResultDto
        {
            KeyId = keyId,
            PlainTextKey = $"tmr_{Guid.NewGuid():N}",
            Key = key,
        });
    }

    public Task<IReadOnlyList<ReportApiKeyDto>> GetApiKeysAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportApiKeyDto>>(
            [.. _apiKeys.Where(key => key.TenantId == tenantId).OrderBy(key => key.CreatedAt)]);

    public Task<CreateReportApiKeyResultDto> RotateApiKeyAsync(string keyId, RotateReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        var index = _apiKeys.FindIndex(key => key.KeyId == keyId && key.TenantId == request.TenantId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Unknown key {keyId}.");
        }

        var previous = _apiKeys[index];
        _apiKeys[index] = previous with { RevokedAt = DateTimeOffset.UtcNow, RevokedByUserId = "Pavel Author", IsActive = false };
        var newKeyId = $"rk_{++_idCounter}";
        var replacement = new ReportApiKeyDto
        {
            KeyId = newKeyId,
            TenantId = previous.TenantId,
            ApplicationId = previous.ApplicationId,
            Permissions = previous.Permissions,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
        };
        _apiKeys.Add(replacement);
        RecordKeyAudit(request.TenantId, newKeyId, "rotate-api-key");
        return Task.FromResult(new CreateReportApiKeyResultDto
        {
            KeyId = newKeyId,
            PlainTextKey = $"tmr_{Guid.NewGuid():N}",
            Key = replacement,
        });
    }

    public Task RevokeApiKeyAsync(string keyId, RevokeReportApiKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        var index = _apiKeys.FindIndex(key => key.KeyId == keyId && key.TenantId == request.TenantId);
        if (index >= 0)
        {
            _apiKeys[index] = _apiKeys[index] with { RevokedAt = DateTimeOffset.UtcNow, RevokedByUserId = "Pavel Author", IsActive = false };
            RecordKeyAudit(request.TenantId, keyId, "revoke-api-key");
        }

        return Task.CompletedTask;
    }

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
    {
        var events = _audit.Where(item => item.TenantId == tenantId);
        if (action is { } a)
        {
            events = events.Where(item => item.Action == a);
        }

        if (outcome is { } o)
        {
            events = events.Where(item => item.Outcome == o);
        }

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            events = events.Where(item => item.ActorId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            events = events.Where(item => item.ResourceId == resourceId);
        }

        var ordered = events.OrderByDescending(item => item.Timestamp).AsEnumerable();
        if (take is { } limit)
        {
            ordered = ordered.Take(limit);
        }

        return Task.FromResult<IReadOnlyList<ReportAuditEventDto>>([.. ordered]);
    }

    public Task<ReportFolderAclEntryDto> GrantPermissionAsync(GrantReportPermissionRequestDto request, CancellationToken cancellationToken = default)
    {
        var entry = new ReportFolderAclEntryDto
        {
            TenantId = request.TenantId,
            FolderId = request.FolderId,
            SubjectKind = request.SubjectKind,
            SubjectId = request.SubjectId,
            Effect = request.Effect,
            Permissions = request.Permissions,
        };
        _acls.RemoveAll(item => item.TenantId == request.TenantId && item.FolderId == request.FolderId
            && item.SubjectKind == request.SubjectKind && item.SubjectId == request.SubjectId);
        _acls.Add(entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<ReportFolderAclEntryDto>> GetFolderPermissionsAsync(string tenantId, string folderId, string? subjectId = null, CancellationToken cancellationToken = default)
    {
        var entries = _acls.Where(item => item.TenantId == tenantId && item.FolderId == folderId);
        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            entries = entries.Where(item => item.SubjectId == subjectId);
        }

        return Task.FromResult<IReadOnlyList<ReportFolderAclEntryDto>>([.. entries.OrderBy(item => item.SubjectId, StringComparer.Ordinal)]);
    }

    public Task RevokePermissionAsync(RevokeReportPermissionRequestDto request, CancellationToken cancellationToken = default)
    {
        _acls.RemoveAll(item => item.TenantId == request.TenantId && item.FolderId == request.FolderId
            && item.SubjectKind == request.SubjectKind && item.SubjectId == request.SubjectId);
        return Task.CompletedTask;
    }

    public Task<ReportResolveResultDto> ResolveReportAsync(string tenantId, string? reportId = null, string? path = null, CancellationToken cancellationToken = default)
    {
        // Mirror the REAL server (ReportServerApiExtensions.ResolveByPathAsync): resolution by id matches
        // ReportId; resolution by path is FOLDER-QUALIFIED, where the LAST segment matches the report NAME
        // (not the id) within the folder whose Path equals "/" + the leading segments. Kept faithful so the
        // portal tests exercise the real /resolve contract instead of a divergent bare-id fake.
        ReportSummaryDto? report;
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            report = _reports.FirstOrDefault(item => item.ReportId == reportId);
        }
        else if (!string.IsNullOrWhiteSpace(path))
        {
            report = ResolveByPath(path);
        }
        else
        {
            throw new KeyNotFoundException("Either reportId or path must be supplied.");
        }

        if (report is null)
        {
            throw new KeyNotFoundException($"Unknown report for reportId='{reportId}' path='{path}'.");
        }

        return Task.FromResult(new ReportResolveResultDto
        {
            TenantId = tenantId,
            ReportId = report.ReportId,
            FolderId = report.FolderId,
            Name = report.Name,
            Description = report.Description,
            LatestRevisionId = report.LatestRevisionId,
            PublishedRevisionId = report.LatestRevisionId,
            RevisionNumber = 1,
            DefinitionJson = "{}",
            RenderPath = "api/render",
        });
    }

    private ReportSummaryDto? ResolveByPath(string path)
    {
        var trimmed = path.Trim().Trim('/');
        var separator = trimmed.LastIndexOf('/');
        if (separator < 0)
        {
            // A folderless path never resolves on the real server (needs a folder segment + report name).
            return null;
        }

        var folderPath = "/" + trimmed[..separator];
        var lastSegment = trimmed[(separator + 1)..];
        var folder = _folders.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, folderPath, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
        {
            return null;
        }

        // Match the last segment against the report's ReportId OR Name (same additive semantics as the
        // real server's ResolveByPathAsync), so both id-based (BuildDeepLink/favorite) and name-based
        // deep links round-trip.
        return _reports.FirstOrDefault(candidate =>
            candidate.FolderId == folder.FolderId
            && (string.Equals(candidate.ReportId, lastSegment, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Name, lastSegment, StringComparison.OrdinalIgnoreCase)));
    }

    // Fáze 12 pass 2 favorites / render-run history: real in-memory behavior + call capture so the
    // portal favorites/run-history/viewer tests exercise the client path meaningfully (not stubs).
    public Task<IReadOnlyList<ReportFavoriteDto>> ListFavoritesAsync(string tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportFavoriteDto>>(
            [.. _favorites.Where(favorite => favorite.TenantId == tenantId).OrderByDescending(favorite => favorite.CreatedAt)]);

    public Task<ReportFavoriteDto> AddFavoriteAsync(AddReportFavoriteRequestDto request, CancellationToken cancellationToken = default)
    {
        AddedFavoriteReportIds.Add(request.ReportId);
        var report = _reports.FirstOrDefault(item => item.ReportId == request.ReportId);
        var favorite = new ReportFavoriteDto
        {
            TenantId = request.TenantId,
            ReportId = request.ReportId,
            ReportName = report?.Name,
            FolderId = report?.FolderId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _favorites.RemoveAll(item => item.TenantId == request.TenantId && item.ReportId == request.ReportId);
        _favorites.Add(favorite);
        return Task.FromResult(favorite);
    }

    public Task RemoveFavoriteAsync(string tenantId, string reportId, CancellationToken cancellationToken = default)
    {
        RemovedFavoriteReportIds.Add(reportId);
        _favorites.RemoveAll(item => item.TenantId == tenantId && item.ReportId == reportId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RenderRunDto>> ListRenderRunsAsync(string tenantId, string? reportId = null, int? max = null, CancellationToken cancellationToken = default)
    {
        var runs = _renderRuns.Where(run => run.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            runs = runs.Where(run => run.ReportId == reportId);
        }

        return Task.FromResult<IReadOnlyList<RenderRunDto>>([.. runs.Take(max ?? int.MaxValue)]);
    }

    /// <summary>Seeds a favorite directly (for list/empty-state tests).</summary>
    public void SeedFavorite(string reportId)
    {
        var report = _reports.FirstOrDefault(item => item.ReportId == reportId);
        _favorites.Add(new ReportFavoriteDto
        {
            TenantId = Tenant,
            ReportId = reportId,
            ReportName = report?.Name,
            FolderId = report?.FolderId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>Seeds a render-run directly (for history list tests).</summary>
    public void SeedRenderRun(string reportId, string format = "Pdf", string outcome = "Succeeded")
        => _renderRuns.Insert(0, new RenderRunDto
        {
            TenantId = Tenant,
            ActorId = "Pavel Author",
            ReportId = reportId,
            Format = format,
            Outcome = outcome,
            PageCount = 3,
            ByteSize = 4096,
            DurationMs = 42,
            CreatedAt = DateTimeOffset.UtcNow,
            ParametersJson = "{}",
        });

    private void RecordKeyAudit(string tenantId, string keyId, string operation)
        => _audit.Add(new ReportAuditEventDto
        {
            TenantId = tenantId,
            ActorId = "Pavel Author",
            Action = ReportAuditActionDto.ChangeAcl,
            ResourceKind = ReportResourceKindDto.Acl,
            ResourceId = keyId,
            Outcome = ReportAuditOutcomeDto.Allowed,
            Timestamp = DateTimeOffset.UtcNow,
            Details = new Dictionary<string, string>(StringComparer.Ordinal) { ["operation"] = operation },
        });

    private static string Slug(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim().ToLowerInvariant();
        var chars = text.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
