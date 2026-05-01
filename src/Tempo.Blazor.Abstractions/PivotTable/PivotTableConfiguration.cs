namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Represents the complete field configuration of a pivot table.
/// </summary>
public sealed class PivotTableConfiguration
{
    /// <summary>Keys of fields placed in the row area.</summary>
    public List<string> RowFieldKeys { get; init; } = [];

    /// <summary>Keys of fields placed in the column area.</summary>
    public List<string> ColumnFieldKeys { get; init; } = [];

    /// <summary>Value field definitions placed in the data area.</summary>
    public List<PivotValueFieldConfiguration> ValueFields { get; init; } = [];

    /// <summary>Filter field keys and their selected values.</summary>
    public Dictionary<string, List<object?>> FilterFields { get; init; } = [];
}

/// <summary>
/// Serializable configuration for a single value field.
/// </summary>
public sealed class PivotValueFieldConfiguration
{
    /// <summary>The source field key.</summary>
    public string FieldKey { get; init; } = string.Empty;

    /// <summary>The aggregation type.</summary>
    public string Aggregation { get; init; } = "Sum";

    /// <summary>The display name override.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The format string.</summary>
    public string? Format { get; init; }

    /// <summary>Optional CSS class applied to the value field header cell.</summary>
    public string? HeaderClass { get; init; }

    /// <summary>Optional inline style applied to the value field header cell.</summary>
    public string? HeaderStyle { get; init; }
}
