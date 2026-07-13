namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Query describing the slice of timeline entries requested from an
/// <see cref="ITimelineDataProvider"/>. Used by TmTimeline / TmActivityTimeline /
/// TmActivityLog to fetch filtered, incrementally paged audit data from a server.
/// </summary>
public sealed class TimelineQuery
{
    /// <summary>
    /// Optional free-text filter. Providers should match it against the author name,
    /// entry content, and entry type (case-insensitive). Null/empty means "no text filter".
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Optional entry-type filter (e.g. "comment", "status_change").
    /// Null/empty means "all types".
    /// </summary>
    public string? EntryType { get; set; }

    /// <summary>Whether internal entries should be included in the result.</summary>
    public bool IncludeInternal { get; set; }

    /// <summary>Number of matching entries to skip before returning items.</summary>
    public int Skip { get; set; }

    /// <summary>
    /// Maximum number of matching entries to return. <see cref="int.MaxValue"/> means
    /// "return everything that matches" (used when no page size is configured).
    /// </summary>
    public int Take { get; set; } = int.MaxValue;
}
