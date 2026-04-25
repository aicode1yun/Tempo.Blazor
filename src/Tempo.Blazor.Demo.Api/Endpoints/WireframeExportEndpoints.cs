using Tempo.Blazor.Abstractions.Wireframe.Export;
using Tempo.Blazor.Demo.Api.Services;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class WireframeExportEndpoints
{
    public static void MapWireframeExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wireframe/export");

        group.MapPost("/png", async (
            WireframeExportRequest request,
            WireframeExportService service,
            CancellationToken cancellationToken) =>
        {
            var png = await service.ExportPngAsync(request, cancellationToken);
            var fileName = $"{request.FileName}.png";
            return Results.File(png, "image/png", fileName);
        });

        group.MapPost("/pdf", async (
            WireframeExportRequest request,
            WireframeExportService service,
            CancellationToken cancellationToken) =>
        {
            var pdf = await service.ExportPdfAsync(request, cancellationToken);
            var fileName = $"{request.FileName}.pdf";
            return Results.File(pdf, "application/pdf", fileName);
        });
    }
}
