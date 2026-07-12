using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.Timeline;

/// <summary>
/// Pure, allocation-light filtering helpers shared by the timeline components and the
/// in-memory timeline data provider. Ordering is intentionally left to the caller so each
/// component preserves its own ordering contract.
/// </summary>
internal static class TimelineFilter
{
    /// <summary>
    /// Applies the internal-visibility, entry-type, and free-text filters to a sequence of entries.
    /// Text is matched (case-insensitive) against author name, plain/HTML content, and entry type.
    /// </summary>
    public static IEnumerable<ITimelineEntry> Apply(
        IEnumerable<ITimelineEntry> entries,
        string? searchText,
        string? entryType,
        bool includeInternal)
    {
        IEnumerable<ITimelineEntry> query = entries.Where(e => includeInternal || !e.IsInternal);

        if (!string.IsNullOrWhiteSpace(entryType))
        {
            var type = entryType.Trim();
            query = query.Where(e => string.Equals(e.EntryType, type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.Trim();
            query = query.Where(e => Matches(e, text));
        }

        return query;
    }

    /// <summary>Returns the distinct, sorted set of entry types present in the entries.</summary>
    public static IReadOnlyList<string> DistinctTypes(IEnumerable<ITimelineEntry> entries) =>
        entries
            .Select(e => e.EntryType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool Matches(ITimelineEntry entry, string text) =>
        Contains(entry.AuthorName, text)
        || Contains(entry.PlainContent, text)
        || Contains(entry.HtmlContent, text)
        || Contains(entry.EntryType, text);

    private static bool Contains(string? value, string text) =>
        value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
