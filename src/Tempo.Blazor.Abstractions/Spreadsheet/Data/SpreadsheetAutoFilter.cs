using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// A single condition of a column filter (operator plus up to two operands).
/// Operands are stored as invariant-culture strings: numeric strings for number filters and
/// ISO-8601 (yyyy-MM-dd) strings for date filters.
/// </summary>
public sealed class SpreadsheetFilterCondition
{
    /// <summary>The comparison operator.</summary>
    public SpreadsheetFilterOperator Operator { get; set; }

    /// <summary>The primary operand (e.g. the search text, the threshold, the lower bound).</summary>
    public string? Operand { get; set; }

    /// <summary>The secondary operand, used by <see cref="SpreadsheetFilterOperator.Between"/> and <see cref="SpreadsheetFilterOperator.NotBetween"/>.</summary>
    public string? Operand2 { get; set; }

    /// <summary>Creates a deep copy of this condition.</summary>
    public SpreadsheetFilterCondition Clone() => new()
    {
        Operator = Operator,
        Operand = Operand,
        Operand2 = Operand2
    };
}

/// <summary>
/// The criteria of a text/number/date column filter: one or two conditions combined with AND/OR.
/// </summary>
public sealed class SpreadsheetFilterCriteria
{
    /// <summary>The conditions to evaluate (one or two).</summary>
    public List<SpreadsheetFilterCondition> Conditions { get; set; } = [];

    /// <summary>How the conditions are combined when there are two.</summary>
    public SpreadsheetFilterJoin Join { get; set; } = SpreadsheetFilterJoin.And;

    /// <summary>Creates a single-condition criteria.</summary>
    public static SpreadsheetFilterCriteria Single(SpreadsheetFilterOperator op, string? operand = null, string? operand2 = null)
        => new() { Conditions = [new SpreadsheetFilterCondition { Operator = op, Operand = operand, Operand2 = operand2 }] };

    /// <summary>Creates a deep copy of this criteria.</summary>
    public SpreadsheetFilterCriteria Clone() => new()
    {
        Join = Join,
        Conditions = Conditions.Select(c => c.Clone()).ToList()
    };
}

/// <summary>A filter that keeps only cells whose background or font colour matches.</summary>
public sealed class SpreadsheetColorFilter
{
    /// <summary>Whether to match the cell fill or the font colour.</summary>
    public SpreadsheetColorTarget Target { get; set; } = SpreadsheetColorTarget.Background;

    /// <summary>The hex colour to match (e.g. <c>#FFFF00</c>).</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Creates a deep copy of this colour filter.</summary>
    public SpreadsheetColorFilter Clone() => new() { Target = Target, Color = Color };
}

/// <summary>
/// The filter applied to a single column of an auto-filtered range.
/// </summary>
public sealed class SpreadsheetColumnFilter
{
    /// <summary>The zero-based sheet column index this filter applies to.</summary>
    public int ColumnIndex { get; set; }

    /// <summary>The kind of filter.</summary>
    public SpreadsheetFilterKind Kind { get; set; } = SpreadsheetFilterKind.Values;

    /// <summary>
    /// For <see cref="SpreadsheetFilterKind.Values"/>: the set of display strings to keep. A row is
    /// kept when its cell display value is contained here. Blank cells are matched by the empty string.
    /// Null means no value restriction.
    /// </summary>
    public HashSet<string>? AllowedValues { get; set; }

    /// <summary>For text/number/date kinds: the criteria to evaluate.</summary>
    public SpreadsheetFilterCriteria? Criteria { get; set; }

    /// <summary>For <see cref="SpreadsheetFilterKind.Color"/>: the colour to match.</summary>
    public SpreadsheetColorFilter? ColorFilter { get; set; }

    /// <summary>Whether this column filter actually restricts anything.</summary>
    public bool IsActive => Kind switch
    {
        SpreadsheetFilterKind.Values => AllowedValues is not null,
        SpreadsheetFilterKind.Color => ColorFilter is not null && !string.IsNullOrEmpty(ColorFilter.Color),
        _ => Criteria is not null && Criteria.Conditions.Count > 0
    };

    /// <summary>Creates a deep copy of this column filter.</summary>
    public SpreadsheetColumnFilter Clone() => new()
    {
        ColumnIndex = ColumnIndex,
        Kind = Kind,
        AllowedValues = AllowedValues is null ? null : new HashSet<string>(AllowedValues, StringComparer.Ordinal),
        Criteria = Criteria?.Clone(),
        ColorFilter = ColorFilter?.Clone()
    };
}

/// <summary>
/// An auto-filter over a rectangular range. The first row of the range is the header row that
/// carries the filter buttons; the rows below are filtered. Column filters are combined with AND.
/// </summary>
public sealed class SpreadsheetAutoFilter
{
    /// <summary>The full range covered by the filter, including the header row.</summary>
    public SpreadsheetRange Range { get; set; }

    /// <summary>The per-column filters currently applied.</summary>
    public List<SpreadsheetColumnFilter> Columns { get; set; } = [];

    /// <summary>Creates an auto-filter over the given range.</summary>
    public SpreadsheetAutoFilter(SpreadsheetRange range)
    {
        Range = range;
    }

    /// <summary>The zero-based sheet index of the header row.</summary>
    public int HeaderRow => Range.StartRow;

    /// <summary>The zero-based sheet index of the first filtered (data) row.</summary>
    public int FirstDataRow => Range.StartRow + 1;

    /// <summary>Returns the active filter for the given column index, or null.</summary>
    public SpreadsheetColumnFilter? GetColumn(int columnIndex)
        => Columns.FirstOrDefault(c => c.ColumnIndex == columnIndex);

    /// <summary>Creates a deep copy of this auto-filter.</summary>
    public SpreadsheetAutoFilter Clone() => new(new SpreadsheetRange(Range.StartRow, Range.StartCol, Range.EndRow, Range.EndCol))
    {
        Columns = Columns.Select(c => c.Clone()).ToList()
    };
}

/// <summary>
/// A distinct value entry returned by the filter engine for the checkbox list in the UI.
/// </summary>
public readonly record struct SpreadsheetFilterValue(string Display, bool IsBlank);
