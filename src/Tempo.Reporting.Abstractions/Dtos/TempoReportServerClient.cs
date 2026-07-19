#pragma warning disable MA0048

using System.Net.Http.Json;
using Tempo.Reporting.Abstractions.Auth;

namespace Tempo.Reporting.Abstractions.Dtos;

/// <summary>
/// Default HTTP implementation of <see cref="ITempoReportServerClient"/>. Derives from
/// <see cref="ApiClientBase"/> so every call attaches the current user's bearer token per request
/// (from the scoped <see cref="IAccessTokenProvider"/>) and retries once after a 401 with a forced
/// token refresh.
/// </summary>
public sealed class TempoReportServerClient : ApiClientBase, ITempoReportServerClient
{
    private readonly string _basePath;

    /// <summary>Creates a typed report server client.</summary>
    public TempoReportServerClient(
        HttpClient httpClient,
        IAccessTokenProvider? accessTokenProvider = null,
        string basePath = "api")
        : base(httpClient, accessTokenProvider)
    {
        _basePath = string.IsNullOrWhiteSpace(basePath) ? "api" : basePath.Trim('/');
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFolderDto>> GetFoldersAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportFolderDto>>(
            $"{_basePath}/folders?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<ReportFolderDto> CreateFolderAsync(
        CreateReportFolderRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<CreateReportFolderRequestDto, ReportFolderDto>(
            $"{_basePath}/folders",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportFolderDto> UpdateFolderAsync(
        string folderId,
        string tenantId,
        UpdateReportFolderRequestDto request,
        CancellationToken cancellationToken = default)
        => await PutAsync<UpdateReportFolderRequestDto, ReportFolderDto>(
            $"{_basePath}/folders/{Uri.EscapeDataString(folderId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportFolderDto> MoveFolderAsync(
        string folderId,
        string tenantId,
        MoveReportFolderRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<MoveReportFolderRequestDto, ReportFolderDto>(
            $"{_basePath}/folders/{Uri.EscapeDataString(folderId)}/move?tenantId={Uri.EscapeDataString(tenantId)}",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DeleteFolderAsync(string folderId, string tenantId, CancellationToken cancellationToken = default)
        => await DeleteAsync(
            $"{_basePath}/folders/{Uri.EscapeDataString(folderId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportSummaryDto>> SearchReportsAsync(
        ReportSearchRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<ReportSearchRequestDto, List<ReportSummaryDto>>(
            $"{_basePath}/reports/search",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportDetailDto> GetReportAsync(
        string reportId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<ReportDetailDto>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Report response was empty.");

    /// <inheritdoc />
    public async Task<ReportDetailDto> CreateReportAsync(
        CreateReportRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<CreateReportRequestDto, ReportDetailDto>(
            $"{_basePath}/reports",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportDetailDto> MoveReportAsync(
        string reportId,
        string tenantId,
        MoveReportRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<MoveReportRequestDto, ReportDetailDto>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}/move?tenantId={Uri.EscapeDataString(tenantId)}",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DeleteReportAsync(string reportId, string tenantId, CancellationToken cancellationToken = default)
        => await DeleteAsync(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportRevisionDto> UpdateReportDefinitionAsync(
        UpdateReportDefinitionRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<UpdateReportDefinitionRequestDto, ReportRevisionDto>(
            $"{_basePath}/reports/{Uri.EscapeDataString(request.ReportId)}/revisions",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportRevisionDto>> GetRevisionsAsync(
        string reportId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportRevisionDto>>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}/revisions?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<ReportRevisionDto> PublishRevisionAsync(
        string reportId,
        string tenantId,
        PublishReportRevisionRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<PublishReportRevisionRequestDto, ReportRevisionDto>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}/publish?tenantId={Uri.EscapeDataString(tenantId)}",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportRevisionDto> RollbackRevisionAsync(
        string reportId,
        string tenantId,
        RollbackReportRevisionRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RollbackReportRevisionRequestDto, ReportRevisionDto>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}/rollback?tenantId={Uri.EscapeDataString(tenantId)}",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportParameterMetadataDto>> GetParametersAsync(
        string reportId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportParameterMetadataDto>>(
            $"{_basePath}/reports/{Uri.EscapeDataString(reportId)}/parameters?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<RenderReportResultDto> RenderAsync(
        RenderReportRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RenderReportRequestDto, RenderReportResultDto>(
            $"{_basePath}/render",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<RenderJobDto> QueueRenderAsync(
        RenderReportRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RenderReportRequestDto, RenderJobDto>(
            $"{_basePath}/render/jobs",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<RenderJobDto> GetRenderJobAsync(
        string jobId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<RenderJobDto>(
            $"{_basePath}/render/jobs/{Uri.EscapeDataString(jobId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Render job response was empty.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportDataSourceDto>> GetDataSourcesAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportDataSourceDto>>(
            $"{_basePath}/datasources?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<ReportDataSourceDto> UpsertDataSourceAsync(
        UpsertReportDataSourceRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<UpsertReportDataSourceRequestDto, ReportDataSourceDto>(
            $"{_basePath}/datasources",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task DeleteDataSourceAsync(string dataSourceId, string tenantId, CancellationToken cancellationToken = default)
        => await DeleteAsync(
            $"{_basePath}/datasources/{Uri.EscapeDataString(dataSourceId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportDataSourceConnectionTestResultDto> TestDataSourceConnectionAsync(
        string dataSourceId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await PostAsync<object, ReportDataSourceConnectionTestResultDto>(
            $"{_basePath}/datasources/{Uri.EscapeDataString(dataSourceId)}/test?tenantId={Uri.EscapeDataString(tenantId)}",
            new { },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportDataSourceSchemaDto> GetDataSourceSchemaAsync(
        string dataSourceId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<ReportDataSourceSchemaDto>(
            $"{_basePath}/datasources/{Uri.EscapeDataString(dataSourceId)}/schema?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? new ReportDataSourceSchemaDto();

    /// <inheritdoc />
    public async Task<ReportDataSourcePreviewDto> PreviewDataSourceAsync(
        string dataSourceId,
        string tenantId,
        int top = 5,
        CancellationToken cancellationToken = default)
        => await GetAsync<ReportDataSourcePreviewDto>(
            $"{_basePath}/datasources/{Uri.EscapeDataString(dataSourceId)}/preview?tenantId={Uri.EscapeDataString(tenantId)}&top={top}",
            cancellationToken).ConfigureAwait(false) ?? new ReportDataSourcePreviewDto();

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportScheduleDto>> GetSchedulesAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportScheduleDto>>(
            $"{_basePath}/schedules?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<ReportScheduleDto?> GetScheduleAsync(
        string tenantId,
        string scheduleId,
        CancellationToken cancellationToken = default)
        => await GetAsync<ReportScheduleDto>(
            $"{_basePath}/schedules/{Uri.EscapeDataString(scheduleId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportScheduleDto> UpsertScheduleAsync(
        UpsertReportScheduleRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<UpsertReportScheduleRequestDto, ReportScheduleDto>(
            $"{_basePath}/schedules",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetScheduleEnabledAsync(
        string scheduleId,
        SetReportScheduleEnabledRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/schedules/{Uri.EscapeDataString(scheduleId)}/enabled")
            {
                Content = JsonContent.Create(request),
            },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DeleteScheduleAsync(string scheduleId, string tenantId, CancellationToken cancellationToken = default)
        => await DeleteAsync(
            $"{_basePath}/schedules/{Uri.EscapeDataString(scheduleId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportScheduleRunDto>> GetScheduleRunsAsync(
        string tenantId,
        string scheduleId,
        int max = 20,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportScheduleRunDto>>(
            $"{_basePath}/schedules/{Uri.EscapeDataString(scheduleId)}/runs?tenantId={Uri.EscapeDataString(tenantId)}&max={max}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<CreateReportApiKeyResultDto> CreateApiKeyAsync(
        CreateReportApiKeyRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<CreateReportApiKeyRequestDto, CreateReportApiKeyResultDto>(
            $"{_basePath}/apikeys",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportApiKeyDto>> GetApiKeysAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportApiKeyDto>>(
            $"{_basePath}/apikeys?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<CreateReportApiKeyResultDto> RotateApiKeyAsync(
        string keyId,
        RotateReportApiKeyRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RotateReportApiKeyRequestDto, CreateReportApiKeyResultDto>(
            $"{_basePath}/apikeys/{Uri.EscapeDataString(keyId)}/rotate",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RevokeApiKeyAsync(
        string keyId,
        RevokeReportApiKeyRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostNoResponseAsync(
            $"{_basePath}/apikeys/{Uri.EscapeDataString(keyId)}/revoke",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportAuditEventDto>> QueryAuditAsync(
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
        var query = new List<string> { $"tenantId={Uri.EscapeDataString(tenantId)}" };
        if (action is { } a)
        {
            query.Add($"action={a}");
        }

        if (outcome is { } o)
        {
            query.Add($"outcome={o}");
        }

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            query.Add($"actorId={Uri.EscapeDataString(actorId)}");
        }

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            query.Add($"resourceId={Uri.EscapeDataString(resourceId)}");
        }

        if (from is { } f)
        {
            query.Add($"from={Uri.EscapeDataString(f.ToString("O"))}");
        }

        if (to is { } t)
        {
            query.Add($"to={Uri.EscapeDataString(t.ToString("O"))}");
        }

        if (take is { } take2)
        {
            query.Add($"take={take2}");
        }

        return await GetAsync<List<ReportAuditEventDto>>(
            $"{_basePath}/audit?{string.Join('&', query)}",
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<ReportFolderAclEntryDto> GrantPermissionAsync(
        GrantReportPermissionRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<GrantReportPermissionRequestDto, ReportFolderAclEntryDto>(
            $"{_basePath}/permissions",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFolderAclEntryDto>> GetFolderPermissionsAsync(
        string tenantId,
        string folderId,
        string? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"tenantId={Uri.EscapeDataString(tenantId)}&folderId={Uri.EscapeDataString(folderId)}";
        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            query += $"&subjectId={Uri.EscapeDataString(subjectId)}";
        }

        return await GetAsync<List<ReportFolderAclEntryDto>>(
            $"{_basePath}/permissions?{query}",
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task RevokePermissionAsync(
        RevokeReportPermissionRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostNoResponseAsync(
            $"{_basePath}/permissions/revoke",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportResolveResultDto> ResolveReportAsync(
        string tenantId,
        string? reportId = null,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"tenantId={Uri.EscapeDataString(tenantId)}";
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            query += $"&reportId={Uri.EscapeDataString(reportId)}";
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            query += $"&path={Uri.EscapeDataString(path)}";
        }

        return await GetAsync<ReportResolveResultDto>(
            $"{_basePath}/resolve?{query}",
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Resolve response was empty.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFavoriteDto>> ListFavoritesAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => await GetAsync<List<ReportFavoriteDto>>(
            $"{_basePath}/favorites?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <inheritdoc />
    public async Task<ReportFavoriteDto> AddFavoriteAsync(
        AddReportFavoriteRequestDto request,
        CancellationToken cancellationToken = default)
        => await PostAsync<AddReportFavoriteRequestDto, ReportFavoriteDto>(
            $"{_basePath}/favorites",
            request,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveFavoriteAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default)
        => await DeleteAsync(
            $"{_basePath}/favorites/{Uri.EscapeDataString(reportId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RenderRunDto>> ListRenderRunsAsync(
        string tenantId,
        string? reportId = null,
        int? max = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"tenantId={Uri.EscapeDataString(tenantId)}";
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            query += $"&reportId={Uri.EscapeDataString(reportId)}";
        }

        if (max is { } m)
        {
            query += $"&max={m}";
        }

        return await GetAsync<List<RenderRunDto>>(
            $"{_basePath}/render/runs?{query}",
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    private async Task PostNoResponseAsync<TRequest>(
        string uri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(request) },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(request) },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("Report server response was empty.");
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string uri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(request) },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("Report server response was empty.");
    }

    private async Task DeleteAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, uri),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<TResponse?> GetAsync<TResponse>(string uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
    }
}

#pragma warning restore MA0048
