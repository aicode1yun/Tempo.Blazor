namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Defines a field that can be used in a pivot table configuration.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public sealed class PivotField<TItem>
{
    /// <summary>Unique identifier for the field.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Display title shown in the UI.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Accessor function to extract the raw value from a data item.</summary>
    public Func<TItem, object?> Accessor { get; init; } = _ => null;

    /// <summary>Optional formatter for displaying the field value.</summary>
    public Func<object?, string>? DisplayFormatter { get; init; }

    /// <summary>The current area where this field is placed.</summary>
    public PivotArea Area { get; set; } = PivotArea.Unused;

    /// <summary>Sort direction applied to this dimension field. Default: None.</summary>
    public PivotSortDirection SortDirection { get; set; } = PivotSortDirection.None;

    /// <summary>Sort criterion for this dimension field. Default: Value.</summary>
    public PivotSortBy SortBy { get; set; } = PivotSortBy.Value;

    /// <summary>
    /// Gets the formatted display value for a given raw value.
    /// </summary>
    public string FormatValue(object? rawValue)
    {
        if (DisplayFormatter is not null)
            return DisplayFormatter(rawValue) ?? string.Empty;

        return rawValue?.ToString() ?? string.Empty;
    }
}
