using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Provides evaluation context for a formula including the source sheet,
/// owning workbook, and sheet index for named range resolution.
/// </summary>
public sealed class FormulaContext
{
    public FormulaContext(SpreadsheetSheet sheet, SpreadsheetWorkbook? workbook = null, int sheetIndex = 0)
    {
        Sheet = sheet;
        Workbook = workbook;
        SheetIndex = sheetIndex;
    }

    public SpreadsheetSheet Sheet { get; }

    /// <summary>The workbook that owns the sheet, used for named range lookup.</summary>
    public SpreadsheetWorkbook? Workbook { get; }

    /// <summary>The zero-based index of the sheet being evaluated.</summary>
    public int SheetIndex { get; }

    /// <summary>Resolves a cell reference to its raw value.</summary>
    public object? ResolveCellRef(string cellRef)
    {
        var cell = Sheet.Cells.GetValueOrDefault(cellRef.Replace("$", "").ToUpperInvariant());
        if (cell is null) return null;
        return cell.Value;
    }

    /// <summary>Resolves a range reference to a list of raw values.</summary>
    public List<object?> ResolveRangeRef(string startRef, string endRef)
    {
        var result = new List<object?>();
        try
        {
            var range = SpreadsheetRange.Parse(startRef + ":" + endRef);
            foreach (var cellRef in range.CellRefs)
            {
                result.Add(ResolveCellRef(cellRef));
            }
        }
        catch
        {
            // Invalid range – return empty list
        }
        return result;
    }

    /// <summary>
    /// Resolves a named range to its underlying A1 reference or constant value.
    /// Sheet-scope names take precedence over workbook-scope names on their sheet.
    /// </summary>
    public string? ResolveNamedRange(string name)
    {
        if (Workbook?.NamedRanges is null || string.IsNullOrWhiteSpace(name))
            return null;

        var match = Workbook.NamedRanges
            .FirstOrDefault(n =>
                string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)
                && n.Scope == NamedRangeScope.Sheet
                && n.SheetIndex == SheetIndex);

        match ??= Workbook.NamedRanges
            .FirstOrDefault(n =>
                string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)
                && n.Scope == NamedRangeScope.Workbook);

        return match?.RefersTo;
    }
}
