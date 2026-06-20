using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Format;

/// <summary>
/// Produces the re-editable text for a cell — the value as the user would type it, rather than the
/// formatted display. A formula returns its expression; a percentage returns e.g. <c>50%</c>; a
/// date/time returns its (re-parseable) formatted form; a number returns its canonical form without
/// thousands separators. This is the inverse intent of <see cref="SpreadsheetValueParser"/>.
/// </summary>
public static class SpreadsheetCellEditText
{
    /// <summary>Returns the editable text for <paramref name="cell"/> using <paramref name="culture"/>.</summary>
    public static string GetEditText(SpreadsheetCell? cell, CultureInfo culture)
    {
        if (cell is null)
            return string.Empty;

        if (!string.IsNullOrEmpty(cell.Formula))
            return cell.Formula!;

        var value = cell.Value;
        if (value is null)
            return string.Empty;

        switch (cell.DataType)
        {
            case SpreadsheetDataType.Percentage when value is double p:
                return (p * 100).ToString("0.###############", culture) + "%";

            case SpreadsheetDataType.Date:
            case SpreadsheetDataType.Time:
            case SpreadsheetDataType.DateTime:
                return SpreadsheetNumberFormatter.Format(value, cell.Style.NumberFormat);

            case SpreadsheetDataType.Boolean when value is bool b:
                return b ? "TRUE" : "FALSE";
        }

        return value switch
        {
            double d => d.ToString("0.###############", culture),
            DateTime => SpreadsheetNumberFormatter.Format(value, cell.Style.NumberFormat),
            bool b => b ? "TRUE" : "FALSE",
            _ => value.ToString() ?? string.Empty
        };
    }
}
