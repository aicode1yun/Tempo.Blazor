namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Specifies the sort direction for a pivot dimension field.
/// </summary>
public enum PivotSortDirection
{
    /// <summary>No explicit sort; natural order is preserved.</summary>
    None,

    /// <summary>Sort in ascending order.</summary>
    Ascending,

    /// <summary>Sort in descending order.</summary>
    Descending
}
