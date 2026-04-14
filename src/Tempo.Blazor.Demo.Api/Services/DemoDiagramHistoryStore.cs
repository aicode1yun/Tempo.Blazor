using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Data;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>EF Core SQLite implementation of <see cref="IDiagramHistoryStore"/>.</summary>
public sealed class DemoDiagramHistoryStore : IDiagramHistoryStore
{
    private readonly DemoDiagramDbContext _dbContext;

    public DemoDiagramHistoryStore(DemoDiagramDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveSnapshotAsync(string diagramId, DiagramDocument document, int version, string? label = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(document, DiagramJsonOptions.Default);
        var existing = await _dbContext.DiagramSnapshots
            .FirstOrDefaultAsync(s => s.DiagramId == diagramId && s.Version == version, cancellationToken);

        if (existing is not null)
        {
            existing.Json = json;
            existing.Label = label;
            existing.SavedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.DiagramSnapshots.Add(new DiagramSnapshotEntity
            {
                DiagramId = diagramId,
                Version = version,
                Label = label,
                Json = json,
                SavedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiagramDocument?> LoadSnapshotAsync(string diagramId, int version, CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.DiagramSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DiagramId == diagramId && s.Version == version, cancellationToken);

        if (snapshot is null) return null;

        return JsonSerializer.Deserialize<DiagramDocument>(snapshot.Json, DiagramJsonOptions.Default);
    }

    public async Task<IReadOnlyList<DiagramHistoryVersion>> GetVersionsAsync(string diagramId, CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.DiagramSnapshots
            .AsNoTracking()
            .Where(s => s.DiagramId == diagramId)
            .OrderByDescending(s => s.Version)
            .Select(s => new DiagramHistoryVersion
            {
                Version = s.Version,
                Label = s.Label,
                SavedAt = s.SavedAt
            })
            .ToListAsync(cancellationToken);

        return versions;
    }

    /// <summary>Lists all distinct diagram IDs that have at least one snapshot.</summary>
    public async Task<IReadOnlyList<DiagramSummaryDto>> GetDiagramsAsync(CancellationToken cancellationToken = default)
    {
        var diagrams = await _dbContext.DiagramSnapshots
            .AsNoTracking()
            .GroupBy(s => s.DiagramId)
            .Select(g => new DiagramSummaryDto
            {
                DiagramId = g.Key,
                LatestVersion = g.Max(s => s.Version),
                LatestSavedAt = g.Max(s => s.SavedAt)
            })
            .OrderByDescending(d => d.LatestSavedAt)
            .ToListAsync(cancellationToken);

        return diagrams;
    }
}
