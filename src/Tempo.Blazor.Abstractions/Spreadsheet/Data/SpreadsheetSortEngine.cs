using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Pure engine that computes the new order of rows (or columns) for a <see cref="SpreadsheetSortSpec"/>.
/// The sort is stable across levels and follows Excel's type ordering via
/// <see cref="SpreadsheetCellValueComparer"/>; blanks always sort last. Colour sorts pin the chosen
/// colour to the active end.
/// </summary>
public static class SpreadsheetSortEngine
{
    /// <summary>
    /// Computes the sorted order of the primary axis. With <see cref="SpreadsheetSortSpec.ByRows"/>
    /// false (default) it returns the data row indices in their new order; with it true it returns
    /// the data column indices. The header row/column, when present, is excluded from the result.
    /// </summary>
    public static IReadOnlyList<int> ComputeOrder(SpreadsheetSheet sheet, SpreadsheetSortSpec spec, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(spec);

        var range = spec.Range;
        var (start, end) = spec.ByRows
            ? (range.StartCol, range.EndCol)
            : (range.StartRow, range.EndRow);

        if (spec.HasHeader)
            start += 1;

        var indices = new List<int>();
        for (var i = start; i <= end; i++)
            indices.Add(i);

        if (spec.Levels.Count == 0 || indices.Count <= 1)
            return indices;

        // Decorate with original position for a stable sort, then compare across levels.
        var decorated = indices.Select((primary, order) => (primary, order)).ToList();
        decorated.Sort((a, b) =>
        {
            foreach (var level in spec.Levels)
            {
                var cmp = CompareOnLevel(sheet, spec, level, a.primary, b.primary, culture);
                if (cmp != 0)
                    return cmp;
            }

            return a.order.CompareTo(b.order); // stable fallback
        });

        return decorated.Select(d => d.primary).ToList();
    }

    private static int CompareOnLevel(
        SpreadsheetSheet sheet,
        SpreadsheetSortSpec spec,
        SpreadsheetSortLevel level,
        int primaryA,
        int primaryB,
        CultureInfo culture)
    {
        var cellA = GetKeyCell(sheet, spec, level.KeyIndex, primaryA);
        var cellB = GetKeyCell(sheet, spec, level.KeyIndex, primaryB);

        var ascending = level.Direction == SpreadsheetSortDirection.Ascending;

        if (level.SortOn is SpreadsheetSortOn.CellColor or SpreadsheetSortOn.FontColor)
        {
            var rank = CompareColor(cellA, cellB, level, ascending);
            return rank;
        }

        var cmp = SpreadsheetCellValueComparer.CompareValues(cellA, cellB, culture, level.CaseSensitive);
        if (cmp == 0)
            return 0;

        // Blanks must remain last regardless of direction.
        var aBlank = SpreadsheetCellValueComparer.Classify(cellA, culture).IsBlank;
        var bBlank = SpreadsheetCellValueComparer.Classify(cellB, culture).IsBlank;
        if (aBlank || bBlank)
            return cmp; // CompareValues already pins blanks last

        return ascending ? cmp : -cmp;
    }

    private static int CompareColor(SpreadsheetCell? a, SpreadsheetCell? b, SpreadsheetSortLevel level, bool ascending)
    {
        var key = NormalizeColor(level.ColorKey);
        var colorA = NormalizeColor(GetColor(a, level.SortOn));
        var colorB = NormalizeColor(GetColor(b, level.SortOn));

        // When a colour key is pinned, matching cells go to the active end (top for ascending).
        if (!string.IsNullOrEmpty(key))
        {
            var matchA = string.Equals(colorA, key, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var matchB = string.Equals(colorB, key, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            if (matchA != matchB)
                return ascending ? matchA.CompareTo(matchB) : matchB.CompareTo(matchA);
            return 0;
        }

        var cmp = string.Compare(colorA, colorB, StringComparison.OrdinalIgnoreCase);
        return ascending ? cmp : -cmp;
    }

    private static string GetColor(SpreadsheetCell? cell, SpreadsheetSortOn sortOn)
        => sortOn == SpreadsheetSortOn.FontColor ? cell?.Style.ForeColor ?? "" : cell?.Style.BackgroundColor ?? "";

    private static SpreadsheetCell? GetKeyCell(SpreadsheetSheet sheet, SpreadsheetSortSpec spec, int keyIndex, int primary)
        => spec.ByRows
            ? sheet.GetCell(keyIndex, primary)
            : sheet.GetCell(primary, keyIndex);

    private static string NormalizeColor(string? color)
        => string.IsNullOrWhiteSpace(color) ? "transparent" : color.Trim();
}
