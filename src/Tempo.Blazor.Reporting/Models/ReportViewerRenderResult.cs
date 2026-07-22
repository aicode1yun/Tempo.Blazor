using Tempo.Reporting.Engine.Snapshot;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Rendered report snapshot with optional refreshed metadata.</summary>
public sealed record ReportViewerRenderResult
{
    /// <summary>Rendered fixed-page snapshot.</summary>
    public ReportSnapshot Snapshot { get; init; } = new();

    /// <summary>Metadata resolved during render.</summary>
    public ReportViewerMetadata? Metadata { get; init; }

    /// <summary>Resolved parameter values used by the render.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue>? Parameters { get; init; }

    /// <summary>Interaction token used for this render.</summary>
    public string? InteractionToken { get; init; }

    /// <summary>Clickable drill-through regions overlaid on the rendered pages by the interactive viewer.</summary>
    public IReadOnlyList<ReportDrillThroughRegion> DrillThroughRegions { get; init; } = [];

    /// <summary>UTC render time.</summary>
    public DateTimeOffset RenderedAt { get; init; } = DateTimeOffset.UtcNow;
}
