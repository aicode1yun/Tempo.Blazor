using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.Spreadsheet.Format;

/// <summary>
/// Formats cell values according to Excel-style number format strings.
/// Supports General, Number, Currency, Percentage, Date, Time, Text and Scientific formats.
/// </summary>
public static partial class SpreadsheetNumberFormatter
{
    private static readonly DateTime ExcelEpoch = new(1899, 12, 30);

    /// <summary>Formats a value using the provided Excel number format string.</summary>
    public static string Format(object? value, string format)
    {
        if (string.IsNullOrWhiteSpace(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase))
            return FormatGeneral(value);

        var fmt = format.Trim();

        // Text format
        if (fmt == "@")
            return value?.ToString() ?? string.Empty;

        // Detect format category
        if (IsDateTimeFormat(fmt))
            return FormatDateTime(value, fmt);

        if (fmt.Contains('%'))
            return FormatPercentage(value, fmt);

        if (IsCurrencyFormat(fmt))
            return FormatCurrency(value, fmt);

        if (fmt.Contains('E', StringComparison.OrdinalIgnoreCase) && fmt.Contains('0'))
            return FormatScientific(value, fmt);

        return FormatNumber(value, fmt);
    }

    private static string FormatGeneral(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string s) return s;
        if (value is bool bv) return bv ? "TRUE" : "FALSE";
        if (value is DateTime dt) return dt.ToString("g", CultureInfo.CurrentCulture);
        if (value is double d) return d.ToString("G", CultureInfo.InvariantCulture);
        if (value is decimal dec) return dec.ToString("G", CultureInfo.InvariantCulture);
        if (value is int or long or short or byte)
            return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
        return value.ToString() ?? string.Empty;
    }

    private static bool IsDateTimeFormat(string fmt)
    {
        var lowered = fmt.ToLowerInvariant();
        return lowered.Contains('y') || lowered.Contains('d') ||
               (lowered.Contains('h') && !lowered.Contains('0')) ||
               lowered.Contains('s') || lowered.Contains("am/pm");
    }

    private static bool IsCurrencyFormat(string fmt)
    {
        return fmt.Contains('$') || fmt.Contains('€') || fmt.Contains('£') ||
               fmt.Contains('¥') || fmt.Contains("Kč") || fmt.Contains("kr");
    }

    private static string FormatDateTime(object? value, string fmt)
    {
        if (!TryGetDateTime(value, out var dt))
            return value?.ToString() ?? string.Empty;

        return dt.ToString(ConvertExcelDateFormat(fmt), CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Converts an Excel-style date/time format string into a .NET custom format, correctly
    /// disambiguating the <c>m</c>/<c>mm</c> token between <b>month</b> and <b>minute</b>:
    /// it is a minute when the nearest preceding time token is an hour (<c>h</c>) or the nearest
    /// following token is a second (<c>s</c>); otherwise it is a month. Excel's lowercase <c>h</c>
    /// renders as 24-hour unless an AM/PM token is present.
    /// </summary>
    private static string ConvertExcelDateFormat(string fmt)
    {
        var hasAmPm = fmt.Contains("AM/PM", StringComparison.OrdinalIgnoreCase)
                      || fmt.Contains("A/P", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder(fmt.Length + 4);
        var i = 0;
        while (i < fmt.Length)
        {
            if (IsAmPmAt(fmt, i, out var ampmLen))
            {
                sb.Append("tt");
                i += ampmLen;
                continue;
            }

            var c = fmt[i];
            var lower = char.ToLowerInvariant(c);
            if (lower is 'd' or 'm' or 'y' or 'h' or 's')
            {
                var j = i;
                while (j < fmt.Length && fmt[j] == c)
                    j++;
                sb.Append(MapDateToken(lower, j - i, fmt, i, j, hasAmPm));
                i = j;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string MapDateToken(char lower, int len, string fmt, int start, int end, bool hasAmPm)
    {
        switch (lower)
        {
            case 'y':
                return len >= 4 ? "yyyy" : "yy";
            case 'd':
                return len switch { >= 4 => "dddd", 3 => "ddd", 2 => "dd", _ => "%d" };
            case 'h':
                var h = hasAmPm ? "h" : "H";
                return len >= 2 ? h + h : "%" + h;
            case 's':
                return len >= 2 ? "ss" : "%s";
            case 'm':
                if (IsMinuteContext(fmt, start, end))
                    return len >= 2 ? "mm" : "%m";
                return len switch { >= 4 => "MMMM", 3 => "MMM", 2 => "MM", _ => "%M" };
            default:
                return fmt.Substring(start, len);
        }
    }

    private static bool IsMinuteContext(string fmt, int start, int end)
    {
        for (var k = start - 1; k >= 0; k--)
        {
            var ch = char.ToLowerInvariant(fmt[k]);
            if (ch == 'h') return true;
            if (ch is 'd' or 'm' or 'y' or 's' || char.IsLetter(fmt[k])) break;
        }
        for (var k = end; k < fmt.Length; k++)
        {
            var ch = char.ToLowerInvariant(fmt[k]);
            if (ch == 's') return true;
            if (ch is 'd' or 'm' or 'y' or 'h' || char.IsLetter(fmt[k])) break;
        }
        return false;
    }

    private static bool IsAmPmAt(string fmt, int i, out int len)
    {
        len = 0;
        if (i + 5 <= fmt.Length && string.Equals(fmt.Substring(i, 5), "AM/PM", StringComparison.OrdinalIgnoreCase))
        {
            len = 5;
            return true;
        }
        if (i + 3 <= fmt.Length && string.Equals(fmt.Substring(i, 3), "A/P", StringComparison.OrdinalIgnoreCase))
        {
            len = 3;
            return true;
        }
        return false;
    }

    private static bool TryGetDateTime(object? value, out DateTime dt)
    {
        dt = default;
        if (value is DateTime d) { dt = d; return true; }
        if (value is double serial) { dt = ExcelEpoch.AddDays(serial); return true; }
        if (value is int i) { dt = ExcelEpoch.AddDays(i); return true; }
        if (value is long l) { dt = ExcelEpoch.AddDays(l); return true; }
        if (value is string s && DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)) return true;
        return false;
    }

    private static string FormatPercentage(object? value, string fmt)
    {
        if (!TryGetDouble(value, out var num))
            return value?.ToString() ?? string.Empty;

        var decimals = CountDecimalPlaces(fmt);
        var symbol = fmt.Contains('%') ? "%" : string.Empty;
        var result = (num * 100).ToString($"F{decimals}", CultureInfo.InvariantCulture);
        return fmt.Replace("0%", "").Replace("%", "").Replace("0", "").Replace(".", "").Replace(",", "").Trim() switch
        {
            _ => $"{result}{symbol}"
        };
    }

    private static string FormatCurrency(object? value, string fmt)
    {
        if (!TryGetDouble(value, out var num))
            return value?.ToString() ?? string.Empty;

        var decimals = CountDecimalPlaces(fmt);
        var currencySymbol = ExtractCurrencySymbol(fmt);
        var result = num.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        // Insert thousand separators
        result = ApplyThousandsSeparator(result, fmt.Contains(','));

        if (fmt.IndexOf(currencySymbol, StringComparison.Ordinal) == 0)
            return currencySymbol + result;
        return result + currencySymbol;
    }

    private static string FormatScientific(object? value, string fmt)
    {
        if (!TryGetDouble(value, out var num))
            return value?.ToString() ?? string.Empty;

        var match = ScientificRegex().Match(fmt);
        var decimalPlaces = match.Groups["dec"].Length;
        return num.ToString($"E{decimalPlaces}", CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(object? value, string fmt)
    {
        if (!TryGetDouble(value, out var num))
            return value?.ToString() ?? string.Empty;

        var decimals = CountDecimalPlaces(fmt);
        var useThousands = fmt.Contains(',');

        var result = num.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        result = ApplyThousandsSeparator(result, useThousands);

        // Handle negative numbers with format sections
        if (num < 0 && fmt.Contains(';'))
        {
            var parts = fmt.Split(';');
            if (parts.Length >= 2)
            {
                // Simplified: just show absolute value for now, color handling would need CSS
                result = Math.Abs(num).ToString($"F{CountDecimalPlaces(parts[1])}", CultureInfo.InvariantCulture);
                result = ApplyThousandsSeparator(result, parts[1].Contains(','));
            }
        }

        return result;
    }

    private static bool TryGetDouble(object? value, out double num)
    {
        num = 0;
        if (value is double d) { num = d; return true; }
        if (value is decimal dec) { num = (double)dec; return true; }
        if (value is int i) { num = i; return true; }
        if (value is long l) { num = l; return true; }
        if (value is float f) { num = f; return true; }
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num)) return true;
        return false;
    }

    private static int CountDecimalPlaces(string fmt)
    {
        var dotIndex = fmt.IndexOf('.');
        if (dotIndex < 0) return 0;
        var count = 0;
        for (var i = dotIndex + 1; i < fmt.Length; i++)
        {
            if (fmt[i] == '0') count++;
            else if (fmt[i] == '#') continue;
            else break;
        }
        return count;
    }

    private static string ExtractCurrencySymbol(string fmt)
    {
        if (fmt.Contains('$')) return "$";
        if (fmt.Contains('€')) return "€";
        if (fmt.Contains('£')) return "£";
        if (fmt.Contains('¥')) return "¥";
        if (fmt.Contains("Kč")) return "Kč";
        if (fmt.Contains("kr")) return "kr";
        return string.Empty;
    }

    private static string ApplyThousandsSeparator(string numberStr, bool useThousands)
    {
        if (!useThousands) return numberStr;

        var parts = numberStr.Split('.');
        var intPart = parts[0];
        if (intPart.StartsWith('-')) intPart = intPart[1..];

        var sb = new StringBuilder();
        var count = 0;
        for (var i = intPart.Length - 1; i >= 0; i--)
        {
            if (count > 0 && count % 3 == 0)
                sb.Insert(0, ',');
            sb.Insert(0, intPart[i]);
            count++;
        }

        if (numberStr.StartsWith('-'))
            sb.Insert(0, '-');

        if (parts.Length > 1)
            sb.Append('.').Append(parts[1]);

        return sb.ToString();
    }

    [GeneratedRegex(@"0\.(?<dec>0+)E\+0", RegexOptions.IgnoreCase)]
    private static partial Regex ScientificRegex();
}
