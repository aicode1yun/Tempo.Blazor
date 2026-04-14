namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Summary of a saved diagram returned by the history API.</summary>
public sealed class DiagramSummaryDto
{
    /// <summary>Diagram identifier.</summary>
    public string DiagramId { get; set; } = string.Empty;

    /// <summary>Latest persisted version number.</summary>
    public int LatestVersion { get; set; }

    /// <summary>UTC timestamp of the most recent save.</summary>
    public DateTime LatestSavedAt { get; set; }
}
