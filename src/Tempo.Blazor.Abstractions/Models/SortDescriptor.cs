namespace Tempo.Blazor.Models;

/// <summary>Sort direction for a data table column.</summary>
public enum DataTableSortDirection
{
    /// <summary>Ascending order.</summary>
    Ascending,

    /// <summary>Descending order.</summary>
    Descending
}

/// <summary>
/// A single sort instruction: the column key to sort by and its direction.
/// In a <see cref="DataTableQuery.SortDescriptors"/> list the position defines precedence
/// (index 0 is the primary sort, index 1 the secondary, and so on).
/// </summary>
/// <param name="Column">Column key (PropertyName or Title) to sort by.</param>
/// <param name="Direction">Sort direction. Defaults to ascending.</param>
public sealed record SortDescriptor(string Column, DataTableSortDirection Direction = DataTableSortDirection.Ascending);
