using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.Demo.Api.Services;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class DiagramHistoryEndpoints
{
    public static void MapDiagramHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diagrams");

        // GET /api/diagrams — list all saved diagrams
        group.MapGet("", async (DemoDiagramHistoryStore store, CancellationToken ct) =>
        {
            var diagrams = await store.GetDiagramsAsync(ct);
            return Results.Ok(diagrams);
        });

        // GET /api/diagrams/{diagramId} — latest version of a diagram
        group.MapGet("{diagramId}", async (string diagramId, DemoDiagramHistoryStore store, CancellationToken ct) =>
        {
            var versions = await store.GetVersionsAsync(diagramId, ct);
            if (versions.Count == 0) return Results.NotFound();
            var latest = await store.LoadSnapshotAsync(diagramId, versions.Max(v => v.Version), ct);
            return latest is not null ? Results.Ok(latest) : Results.NotFound();
        });

        // GET /api/diagrams/{diagramId}/versions — list versions
        group.MapGet("{diagramId}/versions", async (string diagramId, IDiagramHistoryStore store, CancellationToken ct) =>
        {
            var versions = await store.GetVersionsAsync(diagramId, ct);
            return Results.Ok(versions);
        });

        // POST /api/diagrams/{diagramId}/versions — save a new version
        group.MapPost("{diagramId}/versions", async (
            string diagramId,
            SaveVersionRequest request,
            DemoDiagramDbContext dbContext,
            CancellationToken ct) =>
        {
            var nextVersion = await dbContext.DiagramSnapshots
                .Where(s => s.DiagramId == diagramId)
                .Select(s => (int?)s.Version)
                .MaxAsync(ct) ?? 0;

            nextVersion++;

            var json = JsonSerializer.Serialize(request.Document, DiagramJsonOptions.Default);
            dbContext.DiagramSnapshots.Add(new DiagramSnapshotEntity
            {
                DiagramId = diagramId,
                Version = nextVersion,
                Label = request.Label,
                Json = json,
                SavedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(ct);
            return Results.Created($"/api/diagrams/{diagramId}/versions/{nextVersion}", new { Version = nextVersion });
        });

        // GET /api/diagrams/{diagramId}/versions/{version} — load specific version
        group.MapGet("{diagramId}/versions/{version:int}", async (string diagramId, int version, IDiagramHistoryStore store, CancellationToken ct) =>
        {
            var doc = await store.LoadSnapshotAsync(diagramId, version, ct);
            return doc is not null ? Results.Ok(doc) : Results.NotFound();
        });

        // DELETE /api/diagrams/{diagramId} — delete all versions of a diagram
        group.MapDelete("{diagramId}", async (string diagramId, DemoDiagramDbContext dbContext, CancellationToken ct) =>
        {
            var snapshots = await dbContext.DiagramSnapshots
                .Where(s => s.DiagramId == diagramId)
                .ToListAsync(ct);

            if (snapshots.Count == 0) return Results.NotFound();

            dbContext.DiagramSnapshots.RemoveRange(snapshots);
            await dbContext.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}

public sealed class SaveVersionRequest
{
    public DiagramDocument Document { get; set; } = new();
    public string? Label { get; set; }
}
