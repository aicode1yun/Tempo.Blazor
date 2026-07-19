using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>
/// A clickable region projected onto a rendered report page during layout. It anchors an element's
/// <see cref="ReportDrillThroughAction"/> to the element's rectangle and carries the bound data-point
/// field values so a Field-source parameter mapping resolves against the real clicked context.
/// </summary>
public sealed record ReportDrillThroughRegion
{
    /// <summary>One-based page number the region belongs to.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Left coordinate in page pixels.</summary>
    public double X { get; init; }

    /// <summary>Top coordinate in page pixels.</summary>
    public double Y { get; init; }

    /// <summary>Region width in page pixels.</summary>
    public double Width { get; init; }

    /// <summary>Region height in page pixels.</summary>
    public double Height { get; init; }

    /// <summary>Optional accessible label describing the drill-through target.</summary>
    public string? Label { get; init; }

    /// <summary>Drill-through action navigated when the region is clicked.</summary>
    public ReportDrillThroughAction Action { get; init; } = new();

    /// <summary>Field values of the clicked data point, used to evaluate field-sourced parameter mappings.</summary>
    public IReadOnlyDictionary<string, string?> Context { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
