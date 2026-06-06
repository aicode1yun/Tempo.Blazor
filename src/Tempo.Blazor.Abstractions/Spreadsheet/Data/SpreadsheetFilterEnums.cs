namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>The kind of filter applied to a single auto-filter column.</summary>
public enum SpreadsheetFilterKind
{
    /// <summary>A checkbox list of allowed distinct values.</summary>
    Values,

    /// <summary>A text criteria filter (contains, begins with, …).</summary>
    Text,

    /// <summary>A numeric criteria filter (greater than, between, top 10, …).</summary>
    Number,

    /// <summary>A date criteria filter (today, this month, between, …).</summary>
    Date,

    /// <summary>A filter by cell background or font colour.</summary>
    Color
}

/// <summary>The comparison operator used by a single filter condition.</summary>
public enum SpreadsheetFilterOperator
{
    // Text operators
    /// <summary>The text contains the operand.</summary>
    Contains,
    /// <summary>The text does not contain the operand.</summary>
    NotContains,
    /// <summary>The text begins with the operand.</summary>
    BeginsWith,
    /// <summary>The text ends with the operand.</summary>
    EndsWith,

    // Shared equality operators
    /// <summary>The value equals the operand.</summary>
    Equals,
    /// <summary>The value does not equal the operand.</summary>
    NotEquals,

    // Numeric / date comparison operators
    /// <summary>The value is greater than the operand.</summary>
    GreaterThan,
    /// <summary>The value is greater than or equal to the operand.</summary>
    GreaterThanOrEqual,
    /// <summary>The value is less than the operand.</summary>
    LessThan,
    /// <summary>The value is less than or equal to the operand.</summary>
    LessThanOrEqual,
    /// <summary>The value is between the two operands (inclusive).</summary>
    Between,
    /// <summary>The value is not between the two operands.</summary>
    NotBetween,

    // Numeric statistical operators (computed over the column)
    /// <summary>The value is among the top N numeric values (operand = N, default 10).</summary>
    Top10,
    /// <summary>The value is among the bottom N numeric values (operand = N, default 10).</summary>
    Bottom10,
    /// <summary>The value is above the column average.</summary>
    AboveAverage,
    /// <summary>The value is below the column average.</summary>
    BelowAverage,

    // Dynamic date operators (relative to the current date)
    /// <summary>The date is today.</summary>
    Today,
    /// <summary>The date is yesterday.</summary>
    Yesterday,
    /// <summary>The date is tomorrow.</summary>
    Tomorrow,
    /// <summary>The date falls within the current calendar week.</summary>
    ThisWeek,
    /// <summary>The date falls within the current calendar month.</summary>
    ThisMonth,
    /// <summary>The date falls within the current calendar year.</summary>
    ThisYear
}

/// <summary>How two conditions of a single column filter are combined.</summary>
public enum SpreadsheetFilterJoin
{
    /// <summary>Both conditions must match.</summary>
    And,

    /// <summary>Either condition may match.</summary>
    Or
}

/// <summary>Whether a colour filter or colour sort targets the cell fill or the font colour.</summary>
public enum SpreadsheetColorTarget
{
    /// <summary>The cell background fill colour.</summary>
    Background,

    /// <summary>The font (foreground) colour.</summary>
    Font
}
