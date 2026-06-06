using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Classifies and compares spreadsheet cell values using Excel's sort ordering: numbers (and dates)
/// first, then text, then booleans (FALSE &lt; TRUE), then errors, with blanks always sorted last
/// regardless of direction. Shared by the sort and filter engines.
/// </summary>
public static class SpreadsheetCellValueComparer
{
    /// <summary>A type-classified, comparable view of a cell value.</summary>
    public readonly record struct Classified(int Rank, double Number, string Text, bool IsBlank)
    {
        /// <summary>True when the cell carries a numeric (or date) value.</summary>
        public bool IsNumeric => Rank == 0;
    }

    /// <summary>Classifies a cell value for comparison.</summary>
    public static Classified Classify(SpreadsheetCell? cell, CultureInfo culture)
    {
        var value = cell?.Value;
        if (cell is null || value is null || (value is string s0 && s0.Length == 0))
            return new Classified(int.MaxValue, 0, string.Empty, IsBlank: true);

        switch (value)
        {
            case double d:
                return new Classified(0, d, string.Empty, false);
            case int i:
                return new Classified(0, i, string.Empty, false);
            case decimal m:
                return new Classified(0, (double)m, string.Empty, false);
            case DateTime dt:
                return new Classified(0, dt.ToOADate(), string.Empty, false);
            case bool b:
                return new Classified(2, b ? 1 : 0, string.Empty, false);
            case string s when s.StartsWith('#'):
                return new Classified(3, 0, s, false);
            case string s:
                return new Classified(1, 0, s, false);
            default:
                return new Classified(1, 0, value.ToString() ?? string.Empty, false);
        }
    }

    /// <summary>
    /// Compares two cells for an ascending value sort. Blanks always compare greater (sorted last).
    /// </summary>
    public static int CompareValues(SpreadsheetCell? a, SpreadsheetCell? b, CultureInfo culture, bool caseSensitive)
    {
        var ca = Classify(a, culture);
        var cb = Classify(b, culture);

        if (ca.IsBlank || cb.IsBlank)
        {
            if (ca.IsBlank && cb.IsBlank) return 0;
            return ca.IsBlank ? 1 : -1; // blank sorts last
        }

        if (ca.Rank != cb.Rank)
            return ca.Rank.CompareTo(cb.Rank);

        return ca.Rank switch
        {
            0 or 2 => ca.Number.CompareTo(cb.Number),
            _ => string.Compare(ca.Text, cb.Text,
                caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <summary>Returns the display text the filter list shows for a cell (formatted value).</summary>
    public static string GetDisplayText(SpreadsheetCell? cell, CultureInfo culture)
    {
        if (cell is null)
            return string.Empty;

        if (!string.IsNullOrEmpty(cell.DisplayValue))
            return cell.DisplayValue!;

        return SpreadsheetNumberFormatter.Format(cell.Value, cell.Style.NumberFormat) ?? string.Empty;
    }

    /// <summary>Attempts to read a cell's numeric value (number, percentage, currency or date serial).</summary>
    public static bool TryGetNumber(SpreadsheetCell? cell, CultureInfo culture, out double number)
    {
        number = 0;
        switch (cell?.Value)
        {
            case double d: number = d; return true;
            case int i: number = i; return true;
            case decimal m: number = (double)m; return true;
            case DateTime dt: number = dt.ToOADate(); return true;
            case string s when double.TryParse(s, NumberStyles.Any, culture, out var parsed): number = parsed; return true;
            default: return false;
        }
    }

    /// <summary>Attempts to read a cell's date value.</summary>
    public static bool TryGetDate(SpreadsheetCell? cell, CultureInfo culture, out DateTime date)
    {
        date = default;
        switch (cell?.Value)
        {
            case DateTime dt:
                date = dt;
                return true;
            case double d when cell.DataType is SpreadsheetDataType.Date or SpreadsheetDataType.DateTime or SpreadsheetDataType.Time:
                try { date = DateTime.FromOADate(d); return true; } catch { return false; }
            case string s when DateTime.TryParse(s, culture, DateTimeStyles.None, out var parsed):
                date = parsed;
                return true;
            default:
                return false;
        }
    }
}
