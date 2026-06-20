using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>Type of data that a validation rule accepts.</summary>
public enum SpreadsheetValidationType
{
    Any,
    Whole,
    Decimal,
    List,
    Date,
    Time,
    TextLength,
    Custom
}

/// <summary>Comparison operator used in numeric/date/text-length validation rules.</summary>
public enum SpreadsheetValidationOperator
{
    Between,
    NotBetween,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual
}

/// <summary>Determines how an invalid entry is handled.</summary>
public enum SpreadsheetValidationErrorStyle
{
    Stop,
    Warning,
    Information
}

/// <summary>Tooltip shown when the user selects a cell with a validation rule.</summary>
public sealed class SpreadsheetInputMessage
{
    /// <summary>Short title for the tooltip.</summary>
    public string? Title { get; set; }

    /// <summary>Body text of the tooltip.</summary>
    public string? Message { get; set; }

    /// <summary>Creates a shallow copy.</summary>
    public SpreadsheetInputMessage Clone() => new() { Title = Title, Message = Message };
}

/// <summary>Alert shown when the user enters an invalid value.</summary>
public sealed class SpreadsheetValidationErrorAlert
{
    /// <summary>Controls whether invalid input is rejected, warned about or informational.</summary>
    public SpreadsheetValidationErrorStyle Style { get; set; } = SpreadsheetValidationErrorStyle.Stop;

    /// <summary>Short title for the alert dialog.</summary>
    public string? Title { get; set; }

    /// <summary>Body text of the alert dialog.</summary>
    public string? Message { get; set; }

    /// <summary>Creates a shallow copy.</summary>
    public SpreadsheetValidationErrorAlert Clone() => new() { Style = Style, Title = Title, Message = Message };
}

/// <summary>
/// A data validation rule applied to a cell range within a sheet.
/// </summary>
public sealed record SpreadsheetDataValidation
{
    /// <summary>The cell range this rule applies to.</summary>
    public SpreadsheetRange Range { get; set; } = new SpreadsheetRange(0, 0, 0, 0);

    /// <summary>The type of input this rule accepts.</summary>
    public SpreadsheetValidationType Type { get; set; } = SpreadsheetValidationType.Any;

    /// <summary>Comparison operator for numeric / date / text-length rules.</summary>
    public SpreadsheetValidationOperator Operator { get; set; } = SpreadsheetValidationOperator.Between;

    /// <summary>
    /// Primary operand: lower bound (Between/NotBetween), threshold, or for List — a
    /// comma-separated literal list or a range reference starting with <c>=</c>.
    /// </summary>
    public string? Formula1 { get; set; }

    /// <summary>Upper bound for Between / NotBetween operators.</summary>
    public string? Formula2 { get; set; }

    /// <summary>When true, blank cells bypass the rule.</summary>
    public bool AllowBlank { get; set; } = true;

    /// <summary>When true and <see cref="Type"/> is <see cref="SpreadsheetValidationType.List"/>, a dropdown arrow is drawn in the cell.</summary>
    public bool ShowDropDown { get; set; } = true;

    /// <summary>Optional tooltip shown when the cell is selected.</summary>
    public SpreadsheetInputMessage? InputMessage { get; set; }

    /// <summary>Optional alert shown when an invalid value is entered.</summary>
    public SpreadsheetValidationErrorAlert? ErrorAlert { get; set; }

    /// <summary>Creates a deep copy of this validation rule.</summary>
    public SpreadsheetDataValidation DeepClone() => new()
    {
        Range = new SpreadsheetRange(Range.StartRow, Range.StartCol, Range.EndRow, Range.EndCol),
        Type = Type,
        Operator = Operator,
        Formula1 = Formula1,
        Formula2 = Formula2,
        AllowBlank = AllowBlank,
        ShowDropDown = ShowDropDown,
        InputMessage = InputMessage?.Clone(),
        ErrorAlert = ErrorAlert?.Clone()
    };
}
