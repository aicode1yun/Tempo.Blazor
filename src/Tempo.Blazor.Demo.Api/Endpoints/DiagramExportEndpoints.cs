using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Services;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public sealed class DiagramExportRequest
{
    public DiagramDocument Document { get; set; } = new();
    public DiagramExportOptions? Options { get; set; }
}

public static class DiagramExportEndpoints
{
    public static void MapDiagramExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diagram/export");

        group.MapPost("/svg", async (
            DiagramExportRequest request,
            IDiagramExportService service,
            CancellationToken cancellationToken) =>
        {
            var svg = await service.ExportSvgAsync(request.Document, request.Options ?? new DiagramExportOptions(), cancellationToken);
            return Results.Content(svg, "image/svg+xml");
        });

        group.MapPost("/png", async (
            DiagramExportRequest request,
            IDiagramExportService service,
            CancellationToken cancellationToken) =>
        {
            var png = await service.ExportPngAsync(request.Document, request.Options ?? new DiagramExportOptions(), cancellationToken);
            return Results.File(png, "image/png", "diagram.png");
        });

        group.MapPost("/pdf", async (
            DiagramExportRequest request,
            IDiagramExportService service,
            CancellationToken cancellationToken) =>
        {
            var pdf = await service.ExportPdfAsync(request.Document, request.Options ?? new DiagramExportOptions(), cancellationToken);
            return Results.File(pdf, "application/pdf", "diagram.pdf");
        });
    }
}
