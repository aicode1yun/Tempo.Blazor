using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Demo.Api.Data;

/// <summary>EF Core entity for a persisted diagram snapshot.</summary>
public sealed class DiagramSnapshotEntity
{
    public int Id { get; set; }

    /// <summary>Diagram identifier (slug or GUID).</summary>
    public string DiagramId { get; set; } = string.Empty;

    /// <summary>Monotonically increasing version number.</summary>
    public int Version { get; set; }

    /// <summary>Human-readable label (e.g. "Before refactor").</summary>
    public string? Label { get; set; }

    /// <summary>JSON serialized <see cref="DiagramDocument"/>.</summary>
    public string Json { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the version was saved.</summary>
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
