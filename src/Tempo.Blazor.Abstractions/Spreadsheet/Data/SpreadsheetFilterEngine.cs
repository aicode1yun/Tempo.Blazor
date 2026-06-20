using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Pure engine that computes which rows an <see cref="SpreadsheetAutoFilter"/> hides and the distinct
/// values offered in a column's checkbox list. Column filters are combined with logical AND: a data
/// row is hidden when any active column filter rejects its cell. Honours the typed cell values
/// produced in phase 1 so number/date criteria compare correctly.
/// </summary>
public static class SpreadsheetFilterEngine
{
    /// <summary>
    /// Returns the sorted distinct values present in <paramref name="columnIndex"/> across the
    /// filter's data rows. Numbers/dates sort before text; blanks are reported once with
    /// <see cref="SpreadsheetFilterValue.IsBlank"/> set.
    /// </summary>
    public static IReadOnlyList<SpreadsheetFilterValue> DistinctValues(
        SpreadsheetSheet sheet,
        SpreadsheetAutoFilter filter,
        int columnIndex,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(filter);

        var seen = new Dictionary<string, (SpreadsheetCellValueComparer.Classified Key, bool IsBlank)>(StringComparer.Ordinal);
        var hasBlank = false;

        for (var row = filter.FirstDataRow; row <= filter.Range.EndRow; row++)
        {
            var cell = sheet.GetCell(row, columnIndex);
            var classified = SpreadsheetCellValueComparer.Classify(cell, culture);
            if (classified.IsBlank)
            {
                hasBlank = true;
                continue;
            }

            var display = SpreadsheetCellValueComparer.GetDisplayText(cell, culture);
            seen.TryAdd(display, (classified, false));
        }

        var ordered = seen
            .OrderBy(kv => kv.Value.Key.Rank)
            .ThenBy(kv => kv.Value.Key.IsNumeric ? kv.Value.Key.Number : 0d)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new SpreadsheetFilterValue(kv.Key, false))
            .ToList();

        if (hasBlank)
            ordered.Add(new SpreadsheetFilterValue(string.Empty, true));

        return ordered;
    }

    /// <summary>
    /// Computes the zero-based data-row indices that the filter hides. The header row is never hidden.
    /// </summary>
    public static IReadOnlyList<int> ComputeHiddenRows(
        SpreadsheetSheet sheet,
        SpreadsheetAutoFilter filter,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(filter);

        var active = filter.Columns.Where(c => c.IsActive).ToList();
        var hidden = new List<int>();
        if (active.Count == 0)
            return hidden;

        // Pre-compute statistical thresholds (top-N / average) per column once.
        var stats = new Dictionary<int, ColumnStats>();
        foreach (var col in active)
        {
            if (col.Kind == SpreadsheetFilterKind.Number && col.Criteria is not null
                && col.Criteria.Conditions.Any(IsStatistical))
            {
                stats[col.ColumnIndex] = ComputeColumnStats(sheet, filter, col.ColumnIndex, culture);
            }
        }

        for (var row = filter.FirstDataRow; row <= filter.Range.EndRow; row++)
        {
            foreach (var col in active)
            {
                var cell = sheet.GetCell(row, col.ColumnIndex);
                if (!MatchesColumn(cell, col, culture, stats.GetValueOrDefault(col.ColumnIndex)))
                {
                    hidden.Add(row);
                    break; // AND across columns: one rejection hides the row
                }
            }
        }

        return hidden;
    }

    private static bool MatchesColumn(
        SpreadsheetCell? cell,
        SpreadsheetColumnFilter col,
        CultureInfo culture,
        ColumnStats? stats)
        => col.Kind switch
        {
            SpreadsheetFilterKind.Values => MatchesValues(cell, col, culture),
            SpreadsheetFilterKind.Color => MatchesColor(cell, col.ColorFilter),
            _ => MatchesCriteria(cell, col, culture, stats)
        };

    private static bool MatchesValues(SpreadsheetCell? cell, SpreadsheetColumnFilter col, CultureInfo culture)
    {
        if (col.AllowedValues is null)
            return true;

        var display = SpreadsheetCellValueComparer.GetDisplayText(cell, culture);
        return col.AllowedValues.Contains(display);
    }

    private static bool MatchesColor(SpreadsheetCell? cell, SpreadsheetColorFilter? filter)
    {
        if (filter is null || string.IsNullOrEmpty(filter.Color))
            return true;

        var color = filter.Target == SpreadsheetColorTarget.Background
            ? cell?.Style.BackgroundColor
            : cell?.Style.ForeColor;

        return string.Equals(NormalizeColor(color), NormalizeColor(filter.Color), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCriteria(
        SpreadsheetCell? cell,
        SpreadsheetColumnFilter col,
        CultureInfo culture,
        ColumnStats? stats)
    {
        var criteria = col.Criteria;
        if (criteria is null || criteria.Conditions.Count == 0)
            return true;

        bool? result = null;
        foreach (var condition in criteria.Conditions)
        {
            var match = MatchesCondition(cell, col.Kind, condition, culture, stats);
            result = result is null
                ? match
                : criteria.Join == SpreadsheetFilterJoin.And ? result.Value && match : result.Value || match;
        }

        return result ?? true;
    }

    private static bool MatchesCondition(
        SpreadsheetCell? cell,
        SpreadsheetFilterKind kind,
        SpreadsheetFilterCondition condition,
        CultureInfo culture,
        ColumnStats? stats)
        => kind switch
        {
            SpreadsheetFilterKind.Text => MatchesText(cell, condition, culture),
            SpreadsheetFilterKind.Number => MatchesNumber(cell, condition, culture, stats),
            SpreadsheetFilterKind.Date => MatchesDate(cell, condition, culture),
            _ => true
        };

    private static bool MatchesText(SpreadsheetCell? cell, SpreadsheetFilterCondition condition, CultureInfo culture)
    {
        var text = SpreadsheetCellValueComparer.GetDisplayText(cell, culture);
        var operand = condition.Operand ?? string.Empty;
        const StringComparison cmp = StringComparison.OrdinalIgnoreCase;

        return condition.Operator switch
        {
            SpreadsheetFilterOperator.Contains => text.Contains(operand, cmp),
            SpreadsheetFilterOperator.NotContains => !text.Contains(operand, cmp),
            SpreadsheetFilterOperator.BeginsWith => text.StartsWith(operand, cmp),
            SpreadsheetFilterOperator.EndsWith => text.EndsWith(operand, cmp),
            SpreadsheetFilterOperator.Equals => string.Equals(text, operand, cmp),
            SpreadsheetFilterOperator.NotEquals => !string.Equals(text, operand, cmp),
            _ => true
        };
    }

    private static bool MatchesNumber(SpreadsheetCell? cell, SpreadsheetFilterCondition condition, CultureInfo culture, ColumnStats? stats)
    {
        if (!SpreadsheetCellValueComparer.TryGetNumber(cell, culture, out var value))
            return false;

        switch (condition.Operator)
        {
            case SpreadsheetFilterOperator.AboveAverage:
                return stats is { Count: > 0 } && value > stats.Average;
            case SpreadsheetFilterOperator.BelowAverage:
                return stats is { Count: > 0 } && value < stats.Average;
            case SpreadsheetFilterOperator.Top10:
                return stats is not null && stats.TopThreshold is { } top && value >= top;
            case SpreadsheetFilterOperator.Bottom10:
                return stats is not null && stats.BottomThreshold is { } bottom && value <= bottom;
        }

        if (!TryParseNumber(condition.Operand, culture, out var operand))
            return true;

        return condition.Operator switch
        {
            SpreadsheetFilterOperator.Equals => value == operand,
            SpreadsheetFilterOperator.NotEquals => value != operand,
            SpreadsheetFilterOperator.GreaterThan => value > operand,
            SpreadsheetFilterOperator.GreaterThanOrEqual => value >= operand,
            SpreadsheetFilterOperator.LessThan => value < operand,
            SpreadsheetFilterOperator.LessThanOrEqual => value <= operand,
            SpreadsheetFilterOperator.Between => TryParseNumber(condition.Operand2, culture, out var hi) && value >= operand && value <= hi,
            SpreadsheetFilterOperator.NotBetween => !(TryParseNumber(condition.Operand2, culture, out var hi2) && value >= operand && value <= hi2),
            _ => true
        };
    }

    private static bool MatchesDate(SpreadsheetCell? cell, SpreadsheetFilterCondition condition, CultureInfo culture)
    {
        if (!SpreadsheetCellValueComparer.TryGetDate(cell, culture, out var date))
            return false;

        var today = DateTime.Today;
        var day = date.Date;

        switch (condition.Operator)
        {
            case SpreadsheetFilterOperator.Today: return day == today;
            case SpreadsheetFilterOperator.Yesterday: return day == today.AddDays(-1);
            case SpreadsheetFilterOperator.Tomorrow: return day == today.AddDays(1);
            case SpreadsheetFilterOperator.ThisWeek: return IsSameWeek(day, today, culture);
            case SpreadsheetFilterOperator.ThisMonth: return day.Year == today.Year && day.Month == today.Month;
            case SpreadsheetFilterOperator.ThisYear: return day.Year == today.Year;
        }

        if (!TryParseDate(condition.Operand, culture, out var operand))
            return true;

        return condition.Operator switch
        {
            SpreadsheetFilterOperator.Equals => day == operand.Date,
            SpreadsheetFilterOperator.NotEquals => day != operand.Date,
            SpreadsheetFilterOperator.GreaterThan => day > operand.Date,
            SpreadsheetFilterOperator.GreaterThanOrEqual => day >= operand.Date,
            SpreadsheetFilterOperator.LessThan => day < operand.Date,
            SpreadsheetFilterOperator.LessThanOrEqual => day <= operand.Date,
            SpreadsheetFilterOperator.Between => TryParseDate(condition.Operand2, culture, out var hi) && day >= operand.Date && day <= hi.Date,
            SpreadsheetFilterOperator.NotBetween => !(TryParseDate(condition.Operand2, culture, out var hi2) && day >= operand.Date && day <= hi2.Date),
            _ => true
        };
    }

    private static bool IsSameWeek(DateTime a, DateTime b, CultureInfo culture)
    {
        var cal = culture.Calendar;
        var rule = culture.DateTimeFormat.CalendarWeekRule;
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        return a.Year == b.Year
            && cal.GetWeekOfYear(a, rule, firstDay) == cal.GetWeekOfYear(b, rule, firstDay);
    }

    private static bool IsStatistical(SpreadsheetFilterCondition c)
        => c.Operator is SpreadsheetFilterOperator.Top10
            or SpreadsheetFilterOperator.Bottom10
            or SpreadsheetFilterOperator.AboveAverage
            or SpreadsheetFilterOperator.BelowAverage;

    private static ColumnStats ComputeColumnStats(SpreadsheetSheet sheet, SpreadsheetAutoFilter filter, int columnIndex, CultureInfo culture)
    {
        var values = new List<double>();
        for (var row = filter.FirstDataRow; row <= filter.Range.EndRow; row++)
        {
            if (SpreadsheetCellValueComparer.TryGetNumber(sheet.GetCell(row, columnIndex), culture, out var v))
                values.Add(v);
        }

        if (values.Count == 0)
            return new ColumnStats(0, 0, null, null);

        var n = 0;
        foreach (var col in filter.Columns.Where(c => c.ColumnIndex == columnIndex))
        {
            var condition = col.Criteria?.Conditions.FirstOrDefault(c => c.Operator is SpreadsheetFilterOperator.Top10 or SpreadsheetFilterOperator.Bottom10);
            if (condition is not null && int.TryParse(condition.Operand, NumberStyles.Integer, culture, out var parsed) && parsed > 0)
                n = parsed;
        }

        if (n <= 0) n = 10;
        n = Math.Min(n, values.Count);

        var descending = values.OrderByDescending(v => v).ToList();
        var ascending = values.OrderBy(v => v).ToList();
        double topThreshold = descending[n - 1];
        double bottomThreshold = ascending[n - 1];

        return new ColumnStats(values.Count, values.Average(), topThreshold, bottomThreshold);
    }

    private static bool TryParseNumber(string? text, CultureInfo culture, out double value)
        => double.TryParse(text, NumberStyles.Any, culture, out value)
            || double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDate(string? text, CultureInfo culture, out DateTime value)
        => DateTime.TryParse(text, culture, DateTimeStyles.None, out value)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static string NormalizeColor(string? color)
        => string.IsNullOrWhiteSpace(color) ? "transparent" : color.Trim();

    private sealed record ColumnStats(int Count, double Average, double? TopThreshold, double? BottomThreshold);
}
