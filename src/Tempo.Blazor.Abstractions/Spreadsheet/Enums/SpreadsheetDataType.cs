namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>
/// Specifies the data type of a spreadsheet cell value.
/// </summary>
public enum SpreadsheetDataType
{
    /// <summary>A numeric value.</summary>
    Number,

    /// <summary>A text string.</summary>
    Text,

    /// <summary>A boolean value.</summary>
    Boolean,

    /// <summary>A date value.</summary>
    Date,

    /// <summary>A time value.</summary>
    Time,

    /// <summary>A combined date and time value.</summary>
    DateTime,

    /// <summary>An error value (e.g. #DIV/0!, #VALUE!).</summary>
    Error
}
