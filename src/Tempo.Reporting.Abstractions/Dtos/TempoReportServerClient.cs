#pragma warning disable MA0048

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Tempo.Reporting.Abstractions.Dtos;

/// <summary>Default HTTP implementation of <see cref="ITempoReportServerClient"/>.</summary>
public sealed class TempoReportServerClient : ITempoReportServerClient
{
    private readonly HttpClient _httpClient;
    private readonly IReportServerTokenProvider? _tokenProvider;
    private readonly string _basePath;

    /// <summary>Creates a typed report server client.</summary>
    public TempoReportServerClient(
        HttpClient httpClient,
        IReportServerTokenProvider? tokenProvider = null,
        string basePath = "api")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider;
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

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request),
        };
        using var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("Report server response was empty.");
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string uri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(request),
        };
        using var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("Report server response was empty.");
    }

    private async Task DeleteAsync(string uri, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, uri);
        using var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<TResponse?> GetAsync<TResponse>(string uri, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var token = _tokenProvider is null
            ? null
            : await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

#pragma warning restore MA0048
