using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Pure search engine that locates matches for a query within a sheet or workbook, honouring
/// case sensitivity, whole-cell matching and value-vs-formula scope. Returns at most one hit per
/// matching cell (the first occurrence), ordered in reading order (sheet, row, then column).
/// </summary>
public static class SpreadsheetSearchEngine
{
    /// <summary>Finds all matches across a workbook (or the active sheet only, per the scope option).</summary>
    public static IReadOnlyList<SpreadsheetSearchHit> Find(
        SpreadsheetWorkbook workbook,
        int activeSheetIndex,
        SpreadsheetSearchOptions options,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.Query))
            return [];

        if (options.Scope == SpreadsheetSearchScope.Sheet)
        {
            if (activeSheetIndex < 0 || activeSheetIndex >= workbook.Sheets.Count)
                return [];
            return FindInSheet(workbook.Sheets[activeSheetIndex], activeSheetIndex, options, culture);
        }

        var hits = new List<SpreadsheetSearchHit>();
        for (var i = 0; i < workbook.Sheets.Count; i++)
            hits.AddRange(FindInSheet(workbook.Sheets[i], i, options, culture));
        return hits;
    }

    /// <summary>Finds all matches within a single sheet, ordered by row then column.</summary>
    public static IReadOnlyList<SpreadsheetSearchHit> FindInSheet(
        SpreadsheetSheet sheet,
        int sheetIndex,
        SpreadsheetSearchOptions options,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.Query))
            return [];

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var ordered = sheet.Cells
            .Select(kv => (Ref: kv.Key, Cell: kv.Value, Coord: ParseCoord(kv.Key)))
            .OrderBy(x => x.Coord.Row)
            .ThenBy(x => x.Coord.Col);

        var hits = new List<SpreadsheetSearchHit>();
        foreach (var (cellRef, cell, _) in ordered)
        {
            var text = GetSearchableText(cell, options.SearchIn, culture);
            if (text.Length == 0)
                continue;

            if (TryMatch(text, options, comparison, out var start, out var length))
                hits.Add(new SpreadsheetSearchHit(sheetIndex, sheet.Name, cellRef, start, length));
        }

        return hits;
    }

    /// <summary>Returns the text the search engine inspects for a cell under the supplied mode.</summary>
    public static string GetSearchableText(SpreadsheetCell cell, SpreadsheetSearchIn searchIn, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (searchIn == SpreadsheetSearchIn.Formulas)
            return SpreadsheetCellEditText.GetEditText(cell, culture);

        // Values: prefer the cached display value, otherwise format the raw value.
        if (!string.IsNullOrEmpty(cell.DisplayValue))
            return cell.DisplayValue!;

        return SpreadsheetNumberFormatter.Format(cell.Value, cell.Style.NumberFormat) ?? string.Empty;
    }

    /// <summary>
    /// Produces the replacement text for a cell's searchable text. Whole-cell mode replaces the
    /// entire text when it equals the query; otherwise the first occurrence (or every occurrence
    /// when <paramref name="all"/> is true) is replaced. Returns <c>false</c> when nothing matched.
    /// </summary>
    public static bool TryReplace(
        string text,
        SpreadsheetSearchOptions options,
        string replacement,
        bool all,
        out string result)
    {
        ArgumentNullException.ThrowIfNull(options);
        replacement ??= string.Empty;
        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (options.WholeCell)
        {
            if (text.Equals(options.Query, comparison))
            {
                result = replacement;
                return true;
            }

            result = text;
            return false;
        }

        if (string.IsNullOrEmpty(options.Query))
        {
            result = text;
            return false;
        }

        var index = text.IndexOf(options.Query, comparison);
        if (index < 0)
        {
            result = text;
            return false;
        }

        if (!all)
        {
            result = string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + options.Query.Length));
            return true;
        }

        var builder = new System.Text.StringBuilder();
        var cursor = 0;
        while (index >= 0)
        {
            builder.Append(text, cursor, index - cursor);
            builder.Append(replacement);
            cursor = index + options.Query.Length;
            index = text.IndexOf(options.Query, cursor, comparison);
        }

        builder.Append(text, cursor, text.Length - cursor);
        result = builder.ToString();
        return true;
    }

    private static bool TryMatch(
        string text,
        SpreadsheetSearchOptions options,
        StringComparison comparison,
        out int start,
        out int length)
    {
        if (options.WholeCell)
        {
            if (text.Equals(options.Query, comparison))
            {
                start = 0;
                length = text.Length;
                return true;
            }

            start = 0;
            length = 0;
            return false;
        }

        var index = text.IndexOf(options.Query, comparison);
        if (index >= 0)
        {
            start = index;
            length = options.Query.Length;
            return true;
        }

        start = 0;
        length = 0;
        return false;
    }

    private static (int Row, int Col) ParseCoord(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = SpreadsheetRange.ColumnLettersToIndex(letters);
        var row = int.TryParse(numbers, out var r) ? r - 1 : 0;
        return (Math.Max(0, row), Math.Max(0, col));
    }
}
