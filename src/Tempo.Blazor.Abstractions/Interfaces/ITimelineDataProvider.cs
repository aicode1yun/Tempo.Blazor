namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Optional server-side (or async) data provider for TmTimeline, TmActivityTimeline, and
/// TmActivityLog. Implement this to power filtering and incremental ("load more") pagination
/// of audit-scale timelines from an API or database.
/// <para>
/// When no provider is supplied the components fall back to filtering and paging the in-memory
/// <c>Entries</c> list client-side, so existing consumers keep working unchanged. For a ready-made
/// in-memory implementation, use <c>InMemoryTimelineDataProvider</c> from Tempo.Blazor.
/// </para>
/// </summary>
public interface ITimelineDataProvider
{
    /// <summary>
    /// Fetches a filtered, windowed slice of timeline entries for the given <paramref name="query"/>.
    /// </summary>
    /// <param name="query">Filter and paging options for the requested slice.</param>
    /// <param name="ct">Token used to cancel the operation.</param>
    Task<TimelinePage> GetEntriesAsync(TimelineQuery query, CancellationToken ct = default);
}
