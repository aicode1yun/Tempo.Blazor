using System.Globalization;
using System.Text;
using Tempo.Blazor.Components.Spreadsheet.Enums;

namespace Tempo.Blazor.Components.Spreadsheet.Format;

/// <summary>
/// Parses raw cell input text into a typed value, mirroring how Excel / OnlyOffice interpret
/// what the user types. Recognises (in priority order): forced text (leading apostrophe),
/// formulas (<c>=</c>), booleans, percentages, currency, numbers and dates/times — all
/// culture-aware. Also derives an implied number format (e.g. <c>0%</c>, <c>#,##0.00</c>)
/// which the caller applies only when the target cell still uses the <c>General</c> format.
/// </summary>
public static class SpreadsheetValueParser
{
    private static readonly DateTime ExcelEpoch = new(1899, 12, 30);

    // Group-separator space variants: regular space, non-breaking space, narrow non-breaking space.
    private const char Space = ' ';
    private const char Nbsp = ' ';
    private const char NarrowNbsp = ' ';
    private static readonly char[] GroupSpaces = { Space, Nbsp, NarrowNbsp };
    private static readonly string[] CommonCurrencySymbols = { "$", "€", "£", "¥", "Kč", "kr", "zł", "₽" };

    /// <summary>Parses <paramref name="input"/> using <paramref name="culture"/> for number/date conventions.</summary>
    public static SpreadsheetParsedValue Parse(string? input, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(input))
            return SpreadsheetParsedValue.Text(input);

        // 1. Forced text via leading apostrophe (preserves leading zeros, prevents formula/number parsing).
        if (input[0] == '\'')
            return SpreadsheetParsedValue.Text(input[1..], forced: true);

        // 2. Formula.
        if (input.Length > 1 && input[0] == '=')
            return SpreadsheetParsedValue.ForFormula(input);

        var trimmed = input.Trim();
        if (trimmed.Length == 0)
            return SpreadsheetParsedValue.Text(input);

        // 3. Boolean (Excel keeps TRUE/FALSE literals regardless of UI culture).
        if (string.Equals(trimmed, "TRUE", StringComparison.OrdinalIgnoreCase))
            return new SpreadsheetParsedValue { Value = true, Type = SpreadsheetDataType.Boolean };
        if (string.Equals(trimmed, "FALSE", StringComparison.OrdinalIgnoreCase))
            return new SpreadsheetParsedValue { Value = false, Type = SpreadsheetDataType.Boolean };

        // 4. Percentage.
        if (TryParsePercentage(trimmed, culture, out var pct))
            return pct;

        // 5. Currency.
        if (TryParseCurrency(trimmed, culture, out var cur))
            return cur;

        // 6. Number (before date, so e.g. "1.5" stays a number).
        if (TryParseNumericCore(trimmed, culture, out var num, out var hadGroup, out var decimals))
        {
            return new SpreadsheetParsedValue
            {
                Value = num,
                Type = SpreadsheetDataType.Number,
                ImpliedNumberFormat = hadGroup ? BuildGroupFormat(decimals) : null
            };
        }

        // 7. Date / time.
        if (TryParseDateTime(trimmed, culture, out var dt))
            return dt;

        // 8. Fallback: text (original, untrimmed input).
        return SpreadsheetParsedValue.Text(input);
    }

    private static bool TryParsePercentage(string s, CultureInfo culture, out SpreadsheetParsedValue result)
    {
        result = default;
        if (!s.EndsWith('%'))
            return false;

        var numberPart = s[..^1].Trim();
        if (!TryParseNumericCore(numberPart, culture, out var num, out _, out var decimals))
            return false;

        result = new SpreadsheetParsedValue
        {
            Value = num / 100.0,
            Type = SpreadsheetDataType.Percentage,
            ImpliedNumberFormat = decimals > 0 ? "0." + new string('0', decimals) + "%" : "0%"
        };
        return true;
    }

    private static bool TryParseCurrency(string s, CultureInfo culture, out SpreadsheetParsedValue result)
    {
        result = default;

        var symbol = DetectCurrencySymbol(s, culture, out var prefix);
        if (symbol is null)
            return false;

        var numberPart = (prefix ? s[symbol.Length..] : s[..^symbol.Length]).Trim();
        if (!TryParseNumericCore(numberPart, culture, out var num, out _, out var decimals))
            return false;

        var digits = decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
        var format = prefix ? symbol + digits : digits + " " + symbol;

        result = new SpreadsheetParsedValue
        {
            Value = num,
            Type = SpreadsheetDataType.Currency,
            ImpliedNumberFormat = format
        };
        return true;
    }

    private static string? DetectCurrencySymbol(string s, CultureInfo culture, out bool prefix)
    {
        prefix = true;
        var candidates = new List<string>();
        var cultureSymbol = culture.NumberFormat.CurrencySymbol;
        if (!string.IsNullOrWhiteSpace(cultureSymbol))
            candidates.Add(cultureSymbol);
        candidates.AddRange(CommonCurrencySymbols);

        foreach (var sym in candidates)
        {
            if (s.StartsWith(sym, StringComparison.OrdinalIgnoreCase))
            {
                prefix = true;
                return s[..sym.Length];
            }
            if (s.EndsWith(sym, StringComparison.OrdinalIgnoreCase))
            {
                prefix = false;
                return s[^sym.Length..];
            }
        }
        return null;
    }

    /// <summary>
    /// Parses a numeric token culture-aware: removes group separators (spaces and the culture
    /// group separator), converts the culture decimal separator to '.', then parses invariant.
    /// Outputs whether a group separator was present and the number of fractional digits.
    /// </summary>
    private static bool TryParseNumericCore(string s, CultureInfo culture, out double value, out bool hadGroup, out int decimals)
    {
        value = 0;
        hadGroup = false;
        decimals = 0;

        var t = s.Trim();
        if (t.Length == 0)
            return false;

        var decSep = culture.NumberFormat.NumberDecimalSeparator;
        var groupSep = culture.NumberFormat.NumberGroupSeparator;

        var hasSpaceGroup = t.IndexOfAny(GroupSpaces) >= 0;
        var hasCultureGroup = groupSep.Length > 0
            && groupSep != decSep
            && !IsSpace(groupSep)
            && t.Contains(groupSep, StringComparison.Ordinal);
        hadGroup = hasSpaceGroup || hasCultureGroup;

        // Strip group spaces.
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
        {
            if (ch is Space or Nbsp or NarrowNbsp)
                continue;
            sb.Append(ch);
        }
        var normalized = sb.ToString();

        // Strip the (non-space) culture group separator.
        if (hasCultureGroup)
            normalized = normalized.Replace(groupSep, string.Empty);

        // Convert culture decimal separator to invariant '.'.
        if (decSep != ".")
            normalized = normalized.Replace(decSep, ".");

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;

        var dotIdx = normalized.IndexOf('.');
        if (dotIdx >= 0)
        {
            var after = normalized[(dotIdx + 1)..];
            var eIdx = after.IndexOfAny(new[] { 'e', 'E' });
            if (eIdx >= 0)
                after = after[..eIdx];
            decimals = after.Length;
        }
        return true;
    }

    private static bool TryParseDateTime(string s, CultureInfo culture, out SpreadsheetParsedValue result)
    {
        result = default;
        if (!DateTime.TryParse(s, culture, DateTimeStyles.NoCurrentDateDefault, out var dt))
            return false;

        var hasTime = s.Contains(':');
        var hasSeconds = hasTime && s.Count(c => c == ':') >= 2;
        // A date separator distinct from a time colon: any of '.', '/', '-' or a letter (month name).
        var hasDate = s.IndexOfAny(new[] { '.', '/', '-' }) >= 0 || s.Any(char.IsLetter);

        SpreadsheetDataType type;
        DateTime value;
        string fmt;
        var datePattern = BuildDatePattern(culture);
        var timePattern = hasSeconds ? "h:mm:ss" : "h:mm";

        if (hasTime && !hasDate)
        {
            type = SpreadsheetDataType.Time;
            value = ExcelEpoch.Add(dt.TimeOfDay);
            fmt = timePattern;
        }
        else if (hasTime)
        {
            type = SpreadsheetDataType.DateTime;
            value = dt;
            fmt = datePattern + " " + timePattern;
        }
        else
        {
            type = SpreadsheetDataType.Date;
            value = dt.Date;
            fmt = datePattern;
        }

        result = new SpreadsheetParsedValue { Value = value, Type = type, ImpliedNumberFormat = fmt };
        return true;
    }

    private static string BuildGroupFormat(int decimals)
        => decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";

    /// <summary>Builds an Excel-style (lowercase month token) short date pattern for the culture.</summary>
    private static string BuildDatePattern(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern; // e.g. "dd.MM.yyyy" (cs), "M/d/yyyy" (en)
        var sb = new StringBuilder(pattern.Length);
        foreach (var ch in pattern)
            sb.Append(ch == 'M' ? 'm' : char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private static bool IsSpace(string s) => s.Length == 1 && s[0] is Space or Nbsp or NarrowNbsp;
}
