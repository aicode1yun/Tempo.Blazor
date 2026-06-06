namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// The result of aggregating a set of cell values for the status bar, mirroring the
/// OnlyOffice status bar (Sum, Average, Count, Numerical count, Min, Max).
/// </summary>
/// <param name="Count">Number of non-empty cells in the selection.</param>
/// <param name="CountNumbers">Number of numeric cells in the selection.</param>
/// <param name="Sum">Sum of the numeric cells, or <c>null</c> when none are numeric.</param>
/// <param name="Average">Average of the numeric cells, or <c>null</c> when none are numeric.</param>
/// <param name="Min">Minimum numeric value, or <c>null</c> when none are numeric.</param>
/// <param name="Max">Maximum numeric value, or <c>null</c> when none are numeric.</param>
public readonly record struct SpreadsheetAggregationResult(
    int Count,
    int CountNumbers,
    double? Sum,
    double? Average,
    double? Min,
    double? Max)
{
    /// <summary>Whether any numeric aggregation (Sum/Average/Min/Max) is available.</summary>
    public bool HasNumbers => CountNumbers > 0;
}

/// <summary>
/// Computes status-bar aggregations over an arbitrary set of cell values. Numeric aggregations
/// (Sum/Average/Min/Max) ignore text and empty cells; <see cref="SpreadsheetAggregationResult.Count"/>
/// counts every non-empty cell while <see cref="SpreadsheetAggregationResult.CountNumbers"/> counts
/// only numeric cells.
/// </summary>
public static class SpreadsheetAggregation
{
    /// <summary>Aggregates the supplied cell values into a <see cref="SpreadsheetAggregationResult"/>.</summary>
    public static SpreadsheetAggregationResult Compute(IEnumerable<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var count = 0;
        var countNumbers = 0;
        var sum = 0.0;
        var min = double.MaxValue;
        var max = double.MinValue;

        foreach (var value in values)
        {
            if (IsEmpty(value))
                continue;

            count++;

            if (TryToNumber(value, out var number))
            {
                countNumbers++;
                sum += number;
                if (number < min) min = number;
                if (number > max) max = number;
            }
        }

        if (countNumbers == 0)
            return new SpreadsheetAggregationResult(count, 0, null, null, null, null);

        return new SpreadsheetAggregationResult(
            count,
            countNumbers,
            sum,
            sum / countNumbers,
            min,
            max);
    }

    private static bool IsEmpty(object? value)
        => value is null || (value is string s && s.Length == 0);

    private static bool TryToNumber(object? value, out double number)
    {
        switch (value)
        {
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case long l:
                number = l;
                return true;
            case int i:
                number = i;
                return true;
            case short sh:
                number = sh;
                return true;
            case byte b:
                number = b;
                return true;
            case bool:
                // Booleans are not numeric for status-bar aggregation purposes.
                number = 0;
                return false;
            default:
                // Text values (including numeric-looking strings) are not counted as numbers,
                // matching how text-stored numbers are excluded from the OnlyOffice status bar.
                number = 0;
                return false;
        }
    }
}
