namespace Tempo.Blazor.Models;

/// <summary>
/// Encapsulates all parameters for a DataTable data fetch: paging, sorting, filtering, and search.
/// </summary>
public class DataTableQuery
{
    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Primary sort column key (PropertyName or Title). Null = no sort.
    /// Kept for backward compatibility; mirrors the first entry of <see cref="SortDescriptors"/>.
    /// </summary>
    public string? SortColumn { get; init; }

    /// <summary>
    /// True = primary sort descending; false = ascending.
    /// Kept for backward compatibility; mirrors the direction of the first <see cref="SortDescriptors"/> entry.
    /// </summary>
    public bool SortDescending { get; init; }

    /// <summary>
    /// Ordered multi-column sort instructions (index 0 = primary). Empty means fall back to
    /// <see cref="SortColumn"/>/<see cref="SortDescending"/>. Prefer <see cref="GetEffectiveSortDescriptors"/>
    /// which normalizes both representations.
    /// </summary>
    public IReadOnlyList<SortDescriptor> SortDescriptors { get; init; } = [];

    /// <summary>
    /// Returns the effective ordered sort descriptors. Uses <see cref="SortDescriptors"/> when it has
    /// entries; otherwise falls back to a single descriptor built from <see cref="SortColumn"/>/<see cref="SortDescending"/>
    /// (empty when no column is set). Providers should call this to support both single- and multi-column sort.
    /// </summary>
    public IReadOnlyList<SortDescriptor> GetEffectiveSortDescriptors()
    {
        if (SortDescriptors.Count > 0)
        {
            return SortDescriptors;
        }

        return string.IsNullOrEmpty(SortColumn)
            ? []
            : [new SortDescriptor(SortColumn, SortDescending ? DataTableSortDirection.Descending : DataTableSortDirection.Ascending)];
    }

    /// <summary>Column filters to apply.</summary>
    public IReadOnlyList<DataTableFilter> Filters { get; init; } = [];

    /// <summary>Global search text applied across all searchable columns.</summary>
    public string? SearchText { get; init; }

    /// <summary>Columns to group by on the server side.</summary>
    public IReadOnlyList<string> GroupByColumns { get; init; } = [];

    /// <summary>
    /// Per-group page requests. Key = group key (e.g. "Engineering"), Value = 1-based page number.
    /// Null when no specific group page navigation has occurred.
    /// </summary>
    public IReadOnlyDictionary<string, int>? GroupPageRequests { get; init; }
}

/// <summary>Represents a single column filter predicate.</summary>
public record DataTableFilter(string Column, string Operator, object? Value);
