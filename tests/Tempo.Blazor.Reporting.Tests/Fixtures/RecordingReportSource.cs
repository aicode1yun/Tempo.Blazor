using Tempo.Blazor.Reporting.Models;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Blazor.Reporting.Tests.Fixtures;

internal sealed class RecordingReportSource : IReportSource
{
    private readonly ReportSnapshot _snapshot;
    private readonly ReportViewerMetadata _metadata;
    private readonly IReadOnlyDictionary<string, ReportParameterValue>? _renderedParameters;
    private readonly Exception? _renderException;

    public RecordingReportSource(
        ReportSnapshot? snapshot = null,
        ReportViewerMetadata? metadata = null,
        IReadOnlyDictionary<string, ReportParameterValue>? renderedParameters = null,
        Exception? renderException = null)
    {
        _snapshot = snapshot ?? ReportingSnapshots.TwoPageSnapshot();
        _metadata = metadata ?? new ReportViewerMetadata { ReportId = "test", Title = "Test" };
        _renderedParameters = renderedParameters;
        _renderException = renderException;
    }

    public List<ReportViewerMetadataRequest> MetadataRequests { get; } = [];

    public List<ReportViewerRenderRequest> RenderRequests { get; } = [];

    public List<ReportViewerRenderRequest> ExportRequests { get; } = [];

    public List<ReportViewerRenderRequest> CsvExportRequests { get; } = [];

    public List<ReportViewerRenderRequest> XlsxExportRequests { get; } = [];

    public TaskCompletionSource<ReportViewerRenderResult>? RenderGate { get; set; }

    public Task<ReportViewerMetadata> GetMetadataAsync(
        ReportViewerMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        MetadataRequests.Add(request);
        return Task.FromResult(_metadata);
    }

    public async Task<ReportViewerRenderResult> RenderAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        RenderRequests.Add(request);
        if (_renderException is not null)
        {
            throw _renderException;
        }

        if (RenderGate is not null)
        {
            return await RenderGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ReportViewerRenderResult
        {
            Snapshot = _snapshot,
            Metadata = _metadata,
            Parameters = _renderedParameters ?? request.Parameters,
            InteractionToken = request.InteractionToken,
        };
    }

    public Task<ReportViewerExportResult> ExportPdfAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ExportRequests.Add(request);
        return Task.FromResult(new ReportViewerExportResult([0x25, 0x50, 0x44, 0x46], "test.pdf", "application/pdf"));
    }

    public Task<ReportViewerExportResult> ExportCsvAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        CsvExportRequests.Add(request);
        return Task.FromResult(new ReportViewerExportResult(
            System.Text.Encoding.UTF8.GetBytes("Customer,Total\r\nAda,42\r\n"),
            "test.csv",
            "text/csv; charset=utf-8"));
    }

    public Task<ReportViewerExportResult> ExportXlsxAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        XlsxExportRequests.Add(request);
        return Task.FromResult(new ReportViewerExportResult(
            [0x50, 0x4B, 0x03, 0x04],
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }
}
