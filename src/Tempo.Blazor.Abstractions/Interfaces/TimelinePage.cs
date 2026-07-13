namespace Tempo.Blazor.Interfaces;

/// <summary>
/// A filtered, windowed slice of timeline entries returned by an
/// <see cref="ITimelineDataProvider"/>. <see cref="TotalCount"/> lets the component
/// decide whether a "load more" affordance should be offered.
/// </summary>
public sealed class TimelinePage
{
    /// <summary>Entries in this slice — already filtered, ordered, and windowed by the provider.</summary>
    public IReadOnlyList<ITimelineEntry> Items { get; init; } = [];

    /// <summary>Total number of entries matching the query across all pages (before windowing).</summary>
    public int TotalCount { get; init; }
}
