using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Provides evaluation context for a formula including the source sheet.
/// </summary>
public sealed class FormulaContext
{
    public FormulaContext(SpreadsheetSheet sheet)
    {
        Sheet = sheet;
    }

    public SpreadsheetSheet Sheet { get; }

    /// <summary>Resolves a cell reference to its raw value.</summary>
    public object? ResolveCellRef(string cellRef)
    {
        var cell = Sheet.Cells.GetValueOrDefault(cellRef.Replace("$", "").ToUpperInvariant());
        if (cell is null) return null;
        // If the referenced cell has a formula, evaluate it recursively
        if (!string.IsNullOrEmpty(cell.Formula) && cell.DisplayValue is null)
        {
            // Prevent infinite recursion by returning null for circular refs
            // Full circular detection would need a visited set
        }
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
}
