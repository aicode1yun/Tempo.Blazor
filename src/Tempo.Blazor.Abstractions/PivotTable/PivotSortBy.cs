namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Specifies the criterion used when sorting a pivot dimension field.
/// </summary>
public enum PivotSortBy
{
    /// <summary>Sort by the dimension value itself (e.g. alphabetical, numeric).</summary>
    Value,

    /// <summary>Sort by the aggregated measure of the leaf nodes under this dimension.</summary>
    Aggregate
}
