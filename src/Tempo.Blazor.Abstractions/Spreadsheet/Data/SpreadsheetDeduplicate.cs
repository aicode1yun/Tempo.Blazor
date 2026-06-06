using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Pure engine that finds duplicate rows within a range by comparing the values of a chosen set of
/// key columns. The first occurrence of each distinct key combination is kept; every later row with
/// the same combination is reported for removal. Comparison is type-aware (numbers, dates and
/// booleans compare by value, not by their formatted text) and honours case sensitivity for text.
/// </summary>
public static class SpreadsheetDeduplicate
{
    // Unit-separator control char delimits column tokens so ("a","bc") and ("ab","c") never collide.
    private const char ColumnSeparator = (char)0x1F;

    /// <summary>
    /// Returns the absolute row indices that should be removed to deduplicate <paramref name="range"/>
    /// over the given <paramref name="keyColumns"/> (absolute column indices). When
    /// <paramref name="keyColumns"/> is empty every column in the range is used as a key. The header
    /// row is excluded from comparison when <paramref name="hasHeader"/> is set. The result is sorted
    /// ascending and contains the second and subsequent occurrences of each duplicate key.
    /// </summary>
    public static IReadOnlyList<int> ComputeRowsToRemove(
        SpreadsheetSheet sheet,
        SpreadsheetRange range,
        IReadOnlyList<int> keyColumns,
        bool hasHeader,
        bool caseSensitive,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(keyColumns);

        var columns = keyColumns.Count > 0
            ? keyColumns.Where(c => c >= range.StartCol && c <= range.EndCol).Distinct().OrderBy(c => c).ToArray()
            : Enumerable.Range(range.StartCol, range.ColumnCount).ToArray();

        var firstDataRow = hasHeader ? range.StartRow + 1 : range.StartRow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var toRemove = new List<int>();

        for (var row = firstDataRow; row <= range.EndRow; row++)
        {
            var key = BuildRowKey(sheet, row, columns, caseSensitive, culture);
            if (!seen.Add(key))
                toRemove.Add(row);
        }

        return toRemove;
    }

    private static string BuildRowKey(
        SpreadsheetSheet sheet,
        int row,
        IReadOnlyList<int> columns,
        bool caseSensitive,
        CultureInfo culture)
    {
        return string.Join(ColumnSeparator, columns.Select(col => Canonicalize(sheet.GetCell(row, col), caseSensitive, culture)));
    }

    private static string Canonicalize(SpreadsheetCell? cell, bool caseSensitive, CultureInfo culture)
    {
        var value = cell?.Value;
        switch (value)
        {
            case null:
                return string.Empty;
            case double d:
                return "n:" + d.ToString("R", CultureInfo.InvariantCulture);
            case int i:
                return "n:" + ((double)i).ToString("R", CultureInfo.InvariantCulture);
            case decimal m:
                return "n:" + ((double)m).ToString("R", CultureInfo.InvariantCulture);
            case DateTime dt:
                return "d:" + dt.ToOADate().ToString("R", CultureInfo.InvariantCulture);
            case bool b:
                return "b:" + (b ? "1" : "0");
            case string s when s.Length == 0:
                return string.Empty;
            case string s:
                return "t:" + (caseSensitive ? s : s.ToUpper(culture));
            default:
                var text = value.ToString() ?? string.Empty;
                return "t:" + (caseSensitive ? text : text.ToUpper(culture));
        }
    }
}
