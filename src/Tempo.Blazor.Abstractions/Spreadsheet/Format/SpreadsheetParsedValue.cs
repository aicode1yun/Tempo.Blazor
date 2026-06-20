using Tempo.Blazor.Components.Spreadsheet.Enums;

namespace Tempo.Blazor.Components.Spreadsheet.Format;

/// <summary>
/// The result of parsing a raw cell input string via <see cref="SpreadsheetValueParser"/>.
/// Carries the typed value, the detected data type, an optional formula, an optional implied
/// number format (applied only when the target cell still uses the <c>General</c> format), and
/// a flag indicating the input was forced to text with a leading apostrophe.
/// </summary>
public readonly record struct SpreadsheetParsedValue
{
    /// <summary>The typed value (e.g. <see cref="double"/>, <see cref="bool"/>, <see cref="System.DateTime"/>, or <see cref="string"/>). Null for formulas and empty input.</summary>
    public object? Value { get; init; }

    /// <summary>The detected data type of the value.</summary>
    public SpreadsheetDataType Type { get; init; }

    /// <summary>The formula expression (including the leading <c>=</c>) when the input is a formula; otherwise null.</summary>
    public string? Formula { get; init; }

    /// <summary>The number format implied by the input (e.g. <c>0%</c>, <c>#,##0.00</c>), or null when no specific format is implied.</summary>
    public string? ImpliedNumberFormat { get; init; }

    /// <summary>Whether the input was explicitly forced to text via a leading apostrophe.</summary>
    public bool IsForcedText { get; init; }

    /// <summary>Creates a text result with the given string value.</summary>
    public static SpreadsheetParsedValue Text(string? value, bool forced = false) => new()
    {
        Value = value,
        Type = SpreadsheetDataType.Text,
        IsForcedText = forced
    };

    /// <summary>Creates a formula result.</summary>
    public static SpreadsheetParsedValue ForFormula(string formula) => new()
    {
        Value = null,
        Type = SpreadsheetDataType.Number,
        Formula = formula
    };
}
