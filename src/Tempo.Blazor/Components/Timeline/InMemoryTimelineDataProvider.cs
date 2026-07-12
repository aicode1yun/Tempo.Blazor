using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.Timeline;

/// <summary>
/// Client-side <see cref="ITimelineDataProvider"/> that filters, orders (newest-first), and
/// windows an in-memory list. Useful when you want to opt into the provider-based
/// filtering/pagination pipeline without wiring up a server, or as a test double.
/// </summary>
public sealed class InMemoryTimelineDataProvider : ITimelineDataProvider
{
    private readonly IReadOnlyList<ITimelineEntry> _source;

    /// <param name="source">The full in-memory data set.</param>
    public InMemoryTimelineDataProvider(IEnumerable<ITimelineEntry> source)
        => _source = source as IReadOnlyList<ITimelineEntry> ?? source.ToList();

    /// <summary>Filters, orders newest-first, and returns the requested window of entries.</summary>
    /// <param name="query">Filter and paging options for the requested slice.</param>
    /// <param name="ct">Token used to cancel the operation.</param>
    public Task<TimelinePage> GetEntriesAsync(TimelineQuery query, CancellationToken ct = default)
    {
        var filtered = TimelineFilter
            .Apply(_source, query.SearchText, query.EntryType, query.IncludeInternal)
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        var items = filtered
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Max(0, query.Take))
            .ToList();

        return Task.FromResult(new TimelinePage
        {
            Items = items,
            TotalCount = filtered.Count,
        });
    }
}
