#pragma warning disable MA0051

using Tempo.Blazor.Reporting.Models;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Web.Services;

/// <summary>Minimal report server API endpoints used by the demo viewer and remote report source.</summary>
public static class ReportServerApiEndpoints
{
    /// <summary>Maps report metadata, render and export endpoints.</summary>
    public static IEndpointRouteBuilder MapReportServerDemoApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/reports/{reportId}/metadata",
            async (
                string reportId,
                ReportViewerMetadataRequest request,
                DemoReportSourceFactory sourceFactory,
                CancellationToken cancellationToken) =>
            {
                var source = sourceFactory.CreateReportSource(reportId);
                return Results.Ok(await source.GetMetadataAsync(request, cancellationToken).ConfigureAwait(false));
            })
            .RequireReportPermission(
                ReportPermission.View,
                ReportResourceKind.ReportDefinition,
                folderRouteKey: "reportId");

        endpoints.MapPost(
            "/api/reports/{reportId}/render",
            async (
                string reportId,
                ReportViewerRenderRequest request,
                DemoReportSourceFactory sourceFactory,
                CancellationToken cancellationToken) =>
            {
                var source = sourceFactory.CreateReportSource(reportId);
                return Results.Ok(await source.RenderAsync(request, cancellationToken).ConfigureAwait(false));
            })
            .RequireReportPermission(ReportPermission.Render, ReportResourceKind.Render, folderRouteKey: "reportId")
            .WithReportAudit(ReportAuditAction.RenderReport);

        endpoints.MapPost(
            "/api/reports/{reportId}/export/pdf",
            async (
                string reportId,
                ReportViewerRenderRequest request,
                DemoReportSourceFactory sourceFactory,
                CancellationToken cancellationToken) =>
                await ExportAsync(
                    sourceFactory,
                    reportId,
                    request,
                    static (source, renderRequest, token) => source.ExportPdfAsync(renderRequest, token),
                    cancellationToken).ConfigureAwait(false))
            .RequireReportPermission(ReportPermission.Export, ReportResourceKind.Export, folderRouteKey: "reportId")
            .WithReportAudit(ReportAuditAction.ExportReport);

        endpoints.MapPost(
            "/api/reports/{reportId}/export/csv",
            async (
                string reportId,
                ReportViewerRenderRequest request,
                DemoReportSourceFactory sourceFactory,
                CancellationToken cancellationToken) =>
                await ExportAsync(
                    sourceFactory,
                    reportId,
                    request,
                    static (source, renderRequest, token) => source.ExportCsvAsync(renderRequest, token),
                    cancellationToken).ConfigureAwait(false))
            .RequireReportPermission(ReportPermission.Export, ReportResourceKind.Export, folderRouteKey: "reportId")
            .WithReportAudit(ReportAuditAction.ExportReport);

        endpoints.MapPost(
            "/api/reports/{reportId}/export/xlsx",
            async (
                string reportId,
                ReportViewerRenderRequest request,
                DemoReportSourceFactory sourceFactory,
                CancellationToken cancellationToken) =>
                await ExportAsync(
                    sourceFactory,
                    reportId,
                    request,
                    static (source, renderRequest, token) => source.ExportXlsxAsync(renderRequest, token),
                    cancellationToken).ConfigureAwait(false))
            .RequireReportPermission(ReportPermission.Export, ReportResourceKind.Export, folderRouteKey: "reportId")
            .WithReportAudit(ReportAuditAction.ExportReport);

        return endpoints;
    }

    private static async Task<IResult> ExportAsync(
        DemoReportSourceFactory sourceFactory,
        string reportId,
        ReportViewerRenderRequest request,
        Func<IReportSource, ReportViewerRenderRequest, CancellationToken, Task<ReportViewerExportResult>> export,
        CancellationToken cancellationToken)
    {
        var source = sourceFactory.CreateReportSource(reportId);
        var result = await export(source, request, cancellationToken).ConfigureAwait(false);
        return Results.File(result.Bytes, result.ContentType, result.FileName);
    }
}

#pragma warning restore MA0051
