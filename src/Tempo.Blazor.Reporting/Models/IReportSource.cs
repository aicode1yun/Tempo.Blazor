namespace Tempo.Blazor.Reporting.Models;

/// <summary>Report source abstraction used by the Blazor viewer.</summary>
public interface IReportSource
{
    /// <summary>Loads metadata and parameter options for the viewer.</summary>
    Task<ReportViewerMetadata> GetMetadataAsync(
        ReportViewerMetadataRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Renders a report snapshot.</summary>
    Task<ReportViewerRenderResult> RenderAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Exports the current report request as PDF.</summary>
    Task<ReportViewerExportResult> ExportPdfAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Exports the current report request as CSV.</summary>
    Task<ReportViewerExportResult> ExportCsvAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Exports the current report request as XLSX.</summary>
    Task<ReportViewerExportResult> ExportXlsxAsync(
        ReportViewerRenderRequest request,
        CancellationToken cancellationToken = default);
}
