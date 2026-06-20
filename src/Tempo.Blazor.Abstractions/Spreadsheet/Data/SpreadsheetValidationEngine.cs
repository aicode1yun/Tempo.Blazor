using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>Outcome of a single cell validation check.</summary>
public sealed class ValidationResult
{
    /// <summary>True when the value satisfies the rule (or the cell is empty and AllowBlank is set).</summary>
    public bool IsValid { get; init; }

    /// <summary>When false, indicates the error alert style that should be applied.</summary>
    public SpreadsheetValidationErrorStyle ErrorStyle { get; init; } = SpreadsheetValidationErrorStyle.Stop;

    /// <summary>Singleton success result.</summary>
    public static readonly ValidationResult Valid = new() { IsValid = true };

    /// <summary>Constructs a failure result with the given error style.</summary>
    public static ValidationResult Fail(SpreadsheetValidationErrorStyle style) => new() { IsValid = false, ErrorStyle = style };
}

/// <summary>
/// Evaluates whether a candidate cell value satisfies a <see cref="SpreadsheetDataValidation"/> rule.
/// </summary>
public static class SpreadsheetValidationEngine
{
    private static readonly FormulaEngine _formulaEngine = new();

    /// <summary>
    /// Validates <paramref name="cellValue"/> against <paramref name="rule"/>.
    /// Returns <see cref="ValidationResult.Valid"/> when the value is acceptable.
    /// </summary>
    public static ValidationResult Validate(
        object? cellValue,
        SpreadsheetDataValidation rule,
        SpreadsheetSheet sheet,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        var errorStyle = rule.ErrorAlert?.Style ?? SpreadsheetValidationErrorStyle.Stop;

        if (rule.Type == SpreadsheetValidationType.Any)
            return ValidationResult.Valid;

        // Empty / blank handling
        var isEmpty = cellValue is null || (cellValue is string s && string.IsNullOrEmpty(s));
        if (isEmpty)
            return rule.AllowBlank ? ValidationResult.Valid : ValidationResult.Fail(errorStyle);

        return rule.Type switch
        {
            SpreadsheetValidationType.Whole => ValidateNumeric(cellValue, rule, errorStyle, culture, mustBeInteger: true),
            SpreadsheetValidationType.Decimal => ValidateNumeric(cellValue, rule, errorStyle, culture, mustBeInteger: false),
            SpreadsheetValidationType.List => ValidateList(cellValue, rule, errorStyle, sheet),
            SpreadsheetValidationType.Date => ValidateDate(cellValue, rule, errorStyle, culture),
            SpreadsheetValidationType.Time => ValidateTime(cellValue, rule, errorStyle, culture),
            SpreadsheetValidationType.TextLength => ValidateTextLength(cellValue, rule, errorStyle),
            SpreadsheetValidationType.Custom => ValidateCustom(cellValue, rule, errorStyle, sheet),
            _ => ValidationResult.Valid
        };
    }

    /// <summary>
    /// Returns the items that should appear in the in-cell dropdown for a List validation rule.
    /// Handles both literal comma-separated values and range references (starting with =).
    /// </summary>
    public static IReadOnlyList<string> GetListItems(SpreadsheetDataValidation rule, SpreadsheetSheet sheet)
    {
        if (rule.Type != SpreadsheetValidationType.List || string.IsNullOrEmpty(rule.Formula1))
            return Array.Empty<string>();

        var formula1 = rule.Formula1.Trim();

        // Range reference: starts with = (e.g. =$E$1:$E$5)
        if (formula1.StartsWith('='))
        {
            var rangeRef = formula1[1..].Trim();
            try
            {
                var range = SpreadsheetRange.Parse(rangeRef.Replace("$", ""));
                return range.CellRefs
                    .Select(r => sheet.Cells.TryGetValue(r, out var c) ? c.Value?.ToString() : null)
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // Literal list: "Apple,Banana,Cherry" or "Yes,No"
        return formula1.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
    }

    // ────────────────────────────────────────────────────────────────────────

    private static ValidationResult ValidateNumeric(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle,
        CultureInfo culture, bool mustBeInteger)
    {
        if (!TryParseDouble(cellValue, culture, out var value))
            return ValidationResult.Fail(errorStyle);

        if (mustBeInteger && Math.Truncate(value) != value)
            return ValidationResult.Fail(errorStyle);

        if (!TryParseDouble(rule.Formula1, culture, out var f1))
            return ValidationResult.Valid;

        TryParseDouble(rule.Formula2, culture, out var f2);

        return ApplyOperator(value, f1, f2, rule.Operator)
            ? ValidationResult.Valid
            : ValidationResult.Fail(errorStyle);
    }

    private static ValidationResult ValidateList(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle, SpreadsheetSheet sheet)
    {
        var items = GetListItems(rule, sheet);
        var strValue = cellValue?.ToString() ?? string.Empty;
        return items.Any(item => string.Equals(item, strValue, StringComparison.OrdinalIgnoreCase))
            ? ValidationResult.Valid
            : ValidationResult.Fail(errorStyle);
    }

    private static ValidationResult ValidateDate(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle, CultureInfo culture)
    {
        double serialValue;

        if (cellValue is DateTime dt)
        {
            serialValue = DateToSerial(dt);
        }
        else if (cellValue is double d)
        {
            serialValue = d;
        }
        else if (cellValue is string str && DateTime.TryParse(str, culture, DateTimeStyles.None, out var parsed))
        {
            serialValue = DateToSerial(parsed);
        }
        else
        {
            return ValidationResult.Fail(errorStyle);
        }

        if (!TryParseDoubleOrDate(rule.Formula1, culture, out var f1))
            return ValidationResult.Valid;

        TryParseDoubleOrDate(rule.Formula2, culture, out var f2);

        return ApplyOperator(serialValue, f1, f2, rule.Operator)
            ? ValidationResult.Valid
            : ValidationResult.Fail(errorStyle);
    }

    private static ValidationResult ValidateTime(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle, CultureInfo culture)
    {
        double fraction;

        if (cellValue is TimeSpan ts)
        {
            fraction = ts.TotalDays;
        }
        else if (cellValue is double d && d >= 0 && d < 1)
        {
            fraction = d;
        }
        else if (cellValue is string str && TimeSpan.TryParse(str, culture, out var parsedTs))
        {
            fraction = parsedTs.TotalDays;
        }
        else
        {
            return ValidationResult.Fail(errorStyle);
        }

        if (!TryParseTimeSpanFraction(rule.Formula1, culture, out var f1))
            return ValidationResult.Valid;

        TryParseTimeSpanFraction(rule.Formula2, culture, out var f2);

        return ApplyOperator(fraction, f1, f2, rule.Operator)
            ? ValidationResult.Valid
            : ValidationResult.Fail(errorStyle);
    }

    private static ValidationResult ValidateTextLength(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle)
    {
        var len = (double)(cellValue?.ToString()?.Length ?? 0);

        if (!TryParseDouble(rule.Formula1, CultureInfo.InvariantCulture, out var f1))
            return ValidationResult.Valid;

        TryParseDouble(rule.Formula2, CultureInfo.InvariantCulture, out var f2);

        return ApplyOperator(len, f1, f2, rule.Operator)
            ? ValidationResult.Valid
            : ValidationResult.Fail(errorStyle);
    }

    private static ValidationResult ValidateCustom(
        object? cellValue, SpreadsheetDataValidation rule, SpreadsheetValidationErrorStyle errorStyle, SpreadsheetSheet sheet)
    {
        if (string.IsNullOrEmpty(rule.Formula1))
            return ValidationResult.Valid;

        try
        {
            var result = _formulaEngine.Evaluate(rule.Formula1, sheet);
            var pass = result switch
            {
                bool b => b,
                double d => d != 0,
                string s => bool.TryParse(s, out var bv) ? bv : !string.IsNullOrEmpty(s),
                _ => result is not null
            };
            return pass ? ValidationResult.Valid : ValidationResult.Fail(errorStyle);
        }
        catch
        {
            return ValidationResult.Valid;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool ApplyOperator(double value, double f1, double f2, SpreadsheetValidationOperator op) => op switch
    {
        SpreadsheetValidationOperator.Between => value >= f1 && value <= f2,
        SpreadsheetValidationOperator.NotBetween => value < f1 || value > f2,
        SpreadsheetValidationOperator.Equal => value == f1,
        SpreadsheetValidationOperator.NotEqual => value != f1,
        SpreadsheetValidationOperator.GreaterThan => value > f1,
        SpreadsheetValidationOperator.LessThan => value < f1,
        SpreadsheetValidationOperator.GreaterOrEqual => value >= f1,
        SpreadsheetValidationOperator.LessOrEqual => value <= f1,
        _ => true
    };

    private static bool TryParseDouble(object? raw, CultureInfo culture, out double value)
    {
        if (raw is double d) { value = d; return true; }
        if (raw is int i) { value = i; return true; }
        if (raw is long l) { value = l; return true; }
        if (raw is decimal dec) { value = (double)dec; return true; }
        if (raw is string s && double.TryParse(s, NumberStyles.Any, culture, out value)) return true;
        value = 0;
        return false;
    }

    private static bool TryParseDoubleOrDate(string? raw, CultureInfo culture, out double value)
    {
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
        if (DateTime.TryParse(raw, culture, DateTimeStyles.None, out var dt)) { value = DateToSerial(dt); return true; }
        value = 0;
        return false;
    }

    private static bool TryParseTimeSpanFraction(string? raw, CultureInfo culture, out double value)
    {
        if (TimeSpan.TryParse(raw, culture, out var ts)) { value = ts.TotalDays; return true; }
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
        value = 0;
        return false;
    }

    private static double DateToSerial(DateTime dt)
        => (dt - new DateTime(1899, 12, 30)).TotalDays;
}
