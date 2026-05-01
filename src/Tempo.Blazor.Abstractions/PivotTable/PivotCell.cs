namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Represents a single cell in the pivot table matrix.
/// </summary>
public sealed class PivotCell
{
    /// <summary>The raw aggregated value before formatting.</summary>
    public object? RawValue { get; init; }

    /// <summary>The formatted display value.</summary>
    public string FormattedValue { get; init; } = string.Empty;

    /// <summary>Number of source items aggregated into this cell.</summary>
    public int Count { get; init; }

    /// <summary>True when no data exists for this cell combination.</summary>
    public bool IsNull { get; init; }

    /// <summary>Creates an empty/null cell.</summary>
    public static PivotCell Null() => new() { IsNull = true, FormattedValue = string.Empty };
}
