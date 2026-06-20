namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Defines the area where a pivot field is placed within the pivot table configuration.
/// </summary>
public enum PivotArea
{
    /// <summary>Field is not assigned to any area.</summary>
    Unused,

    /// <summary>Field is placed in row headers.</summary>
    Row,

    /// <summary>Field is placed in column headers.</summary>
    Column,

    /// <summary>Field is used as a data/aggregation value.</summary>
    Data,

    /// <summary>Field is used as a filter.</summary>
    Filter
}
