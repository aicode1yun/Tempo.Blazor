using Tempo.Blazor.Models;

namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Defines a data/aggregation field in the pivot table values area.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public sealed class PivotValueField<TItem>
{
    /// <summary>The source field key this value field references.</summary>
    public string FieldKey { get; init; } = string.Empty;

    /// <summary>Display name shown in the column/row header.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The aggregation function to apply.</summary>
    public AggregateType Aggregation { get; set; } = AggregateType.Sum;

    /// <summary>Optional .NET format string (e.g. "C", "N2", "P").</summary>
    public string? Format { get; set; }

    /// <summary>Show the value in the pivot table. Default true.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Formats a raw aggregated value using the configured format string.
    /// </summary>
    public string FormatValue(object? rawValue)
    {
        if (rawValue is null)
            return string.Empty;

        if (!string.IsNullOrEmpty(Format) && rawValue is IFormattable formattable)
            return formattable.ToString(Format, System.Globalization.CultureInfo.CurrentCulture);

        return rawValue.ToString() ?? string.Empty;
    }
}
