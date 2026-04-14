using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Persists diagram snapshots and version history.</summary>
/// <remarks>Concrete implementations are provided by the consuming application.</remarks>
public interface IDiagramHistoryStore
{
    /// <summary>Saves a document snapshot.</summary>
    Task SaveSnapshotAsync(string diagramId, DiagramDocument document, int version, string? label = null, CancellationToken cancellationToken = default);

    /// <summary>Loads a specific version of a diagram.</summary>
    Task<DiagramDocument?> LoadSnapshotAsync(string diagramId, int version, CancellationToken cancellationToken = default);

    /// <summary>Lists all saved versions for a diagram.</summary>
    Task<IReadOnlyList<DiagramHistoryVersion>> GetVersionsAsync(string diagramId, CancellationToken cancellationToken = default);
}

/// <summary>Metadata about a persisted diagram version.</summary>
public sealed class DiagramHistoryVersion
{
    /// <summary>Version number (monotonically increasing).</summary>
    public int Version { get; set; }

    /// <summary>Optional user-provided label.</summary>
    public string? Label { get; set; }

    /// <summary>UTC timestamp when the version was saved.</summary>
    public DateTime SavedAt { get; set; }
}
