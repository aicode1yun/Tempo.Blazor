using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.Timeline;

/// <summary>
/// Drives filtering + incremental ("load more") pagination for the timeline components.
/// This is not a component; a timeline owns one as a field and reconfigures it on each
/// parameters-set. It transparently uses an <see cref="ITimelineDataProvider"/> when one is
/// supplied, otherwise it filters and windows the in-memory <see cref="Source"/> list.
/// </summary>
internal sealed class TimelinePager
{
    // ── Configuration (set by the owning component before each reload) ─────────
    public IEnumerable<ITimelineEntry> Source { get; set; } = [];
    public ITimelineDataProvider? Provider { get; set; }
    public string? SearchText { get; set; }
    public string? EntryType { get; set; }
    public bool IncludeInternal { get; set; }

    /// <summary>Page size for incremental loading; <c>null</c> shows everything (no paging).</summary>
    public int? PageSize { get; set; }

    /// <summary>Optional ordering applied to the client-side path before windowing.</summary>
    public Func<IEnumerable<ITimelineEntry>, IEnumerable<ITimelineEntry>>? Order { get; set; }

    // ── State ──────────────────────────────────────────────────────────────────
    private int _window = 1;
    private IReadOnlyList<ITimelineEntry> _items = [];
    private int _totalCount;

    /// <summary>Entries currently visible (filtered + windowed).</summary>
    public IReadOnlyList<ITimelineEntry> Items => _items;

    /// <summary>Number of entries currently visible.</summary>
    public int ShownCount => _items.Count;

    /// <summary>Total number of entries matching the current filter across all pages.</summary>
    public int TotalCount => _totalCount;

    /// <summary>True when more entries are available beyond the current window.</summary>
    public bool HasMore => _items.Count < _totalCount;

    /// <summary>Resets the incremental window back to the first page. Call when the filter changes.</summary>
    public void ResetWindow() => _window = 1;

    /// <summary>
    /// Recomputes <see cref="Items"/> for the current filter and window. Resolves the
    /// in-memory path synchronously and only awaits when a <see cref="Provider"/> is set.
    /// </summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (Provider is not null)
        {
            var page = await Provider.GetEntriesAsync(
                new TimelineQuery
                {
                    SearchText = SearchText,
                    EntryType = EntryType,
                    IncludeInternal = IncludeInternal,
                    Skip = 0,
                    Take = ComputeTake(),
                },
                ct).ConfigureAwait(false);

            _items = page.Items;
            _totalCount = page.TotalCount;
            return;
        }

        ReloadSync();
    }

    /// <summary>
    /// Recomputes <see cref="Items"/> from the in-memory <see cref="Source"/> only.
    /// Callers must guard on <c>Provider == null</c> before using this.
    /// </summary>
    public void ReloadSync()
    {
        var take = ComputeTake();

        IEnumerable<ITimelineEntry> filtered =
            TimelineFilter.Apply(Source, SearchText, EntryType, IncludeInternal);
        if (Order is not null)
        {
            filtered = Order(filtered);
        }

        var list = filtered.ToList();
        _totalCount = list.Count;
        _items = take >= list.Count ? list : list.Take(take).ToList();
    }

    /// <summary>Grows the window by one page and reloads.</summary>
    public Task LoadMoreAsync(CancellationToken ct = default)
    {
        if (PageSize.HasValue)
        {
            _window++;
        }

        return ReloadAsync(ct);
    }

    private int ComputeTake()
    {
        if (!PageSize.HasValue)
        {
            return int.MaxValue;
        }

        var requested = (long)PageSize.Value * _window;
        return requested >= int.MaxValue ? int.MaxValue : (int)requested;
    }
}
