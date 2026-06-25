using System.Net.Http.Json;
using Tempo.Blazor.Reporting.Interop;
using Tempo.Blazor.Reporting.Models;

namespace Tempo.Blazor.Reporting.Services;

/// <summary>HTTP-backed report source for Tempo Report Server endpoints.</summary>
public sealed class RemoteReportSource : IReportSource
{
    private readonly HttpClient _httpClient;
    private readonly string _reportId;
    private readonly string _basePath;

    /// <summary>Creates a remote report source.</summary>
    public RemoteReportSource(HttpClient httpClient, string reportId, string basePath = "api/reports")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _reportId = string.IsNullOrWhiteSpace(reportId) ? throw new ArgumentException("Report id is required.", nameof(reportId)) : reportId;
        _basePath = string.IsNullOrWhiteSpace(basePath) ? "api/reports" : basePath.TrimEnd('/');
    }

    /// <inheritdoc />
    public async Task<ReportViewerMetadata> GetMetadataAsync(
        ReportViewerMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            $"{_basePath}/{Uri.EscapeDataString(_reportId)}/metadata",
            request,
            ReportViewerJson.Options,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReportViewerMetadata>(
            ReportViewerJson.Options,
            cancellationToken).ConfigureAwait(false) ?? new ReportViewerMetadata { ReportId = _reportId };
    }

    /// <inheritdoc />
    public async Task<ReportViewerRenderResult> RenderAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            $"{_basePath}/{Uri.EscapeDataString(_reportId)}/render",
            request,
            ReportViewerJson.Options,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReportViewerRenderResult>(
            ReportViewerJson.Options,
            cancellationToken).ConfigureAwait(false) ?? new ReportViewerRenderResult();
    }

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportPdfAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
        => await ExportAsync("pdf", "application/pdf", request, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportCsvAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
        => await ExportAsync("csv", "text/csv", request, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportViewerExportResult> ExportXlsxAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
        => await ExportAsync(
            "xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            request,
            cancellationToken).ConfigureAwait(false);

    private async Task<ReportViewerExportResult> ExportAsync(
        string format,
        string fallbackContentType,
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            $"{_basePath}/{Uri.EscapeDataString(_reportId)}/export/{format}",
            request,
            ReportViewerJson.Options,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ??
            $"{_reportId}.{format}";
        return new ReportViewerExportResult(
            await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false),
            fileName,
            response.Content.Headers.ContentType?.MediaType ?? fallbackContentType);
    }
}
