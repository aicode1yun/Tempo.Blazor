using Tempo.Blazor.Components.Spreadsheet.Formula;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents the root workbook containing one or more spreadsheet sheets.
/// </summary>
public sealed class SpreadsheetWorkbook
{
    /// <summary>All sheets in the workbook.</summary>
    public List<SpreadsheetSheet> Sheets { get; set; } = new();

    /// <summary>The sheet currently being displayed or edited.</summary>
    public SpreadsheetSheet? ActiveSheet => Sheets.Count > 0 && ActiveSheetIndex >= 0 && ActiveSheetIndex < Sheets.Count
        ? Sheets[ActiveSheetIndex]
        : null;

    /// <summary>The zero-based index of the active sheet.</summary>
    public int ActiveSheetIndex { get; set; }

    /// <summary>Named ranges defined in this workbook.</summary>
    public List<SpreadsheetNamedRange> NamedRanges { get; set; } = new();

    /// <summary>Creates a new workbook with a single default sheet.</summary>
    public SpreadsheetWorkbook()
    {
        AddSheet("Sheet1");
    }

    /// <summary>Adds a new sheet with the given name.</summary>
    public SpreadsheetSheet AddSheet(string name)
    {
        var sheet = new SpreadsheetSheet { Name = name, Workbook = this, SheetIndexInWorkbook = Sheets.Count };
        Sheets.Add(sheet);
        if (Sheets.Count == 1)
            ActiveSheetIndex = 0;
        return sheet;
    }

    /// <summary>Removes the sheet at the given index.</summary>
    public void RemoveSheet(int index)
    {
        if (index < 0 || index >= Sheets.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Sheets.RemoveAt(index);

        // Keep each remaining sheet's cached index in sync so Sheet-scope named ranges resolve correctly.
        for (int i = index; i < Sheets.Count; i++)
            Sheets[i].SheetIndexInWorkbook = i;

        if (ActiveSheetIndex >= Sheets.Count)
            ActiveSheetIndex = Math.Max(0, Sheets.Count - 1);
    }

    /// <summary>Removes the sheet with the given name.</summary>
    public void RemoveSheet(string name)
    {
        var index = Sheets.FindIndex(s => s.Name == name);
        if (index < 0)
            throw new ArgumentException($"Sheet '{name}' not found.", nameof(name));

        RemoveSheet(index);
    }

    /// <summary>
    /// Re-evaluates every formula in the workbook that references the given named range,
    /// then recalculates all of their transitive dependents.
    /// </summary>
    public void RecalculateNamedRangeDependents(string namedRangeName)
    {
        var nameUpper = namedRangeName.ToUpperInvariant();
        foreach (var sheet in Sheets)
        {
            foreach (var (cellRef, cell) in sheet.Cells)
            {
                if (string.IsNullOrEmpty(cell.Formula))
                    continue;

                var names = FormulaDependencyExtractor.ExtractNamedRanges(cell.Formula);
                if (names.Contains(nameUpper))
                {
                    sheet.EvaluateFormula(cellRef);
                    sheet.RecalculateDependents(cellRef);
                }
            }
        }
    }

    /// <summary>Creates a deep copy of this workbook including all sheets and cells.</summary>
    public SpreadsheetWorkbook Clone()
    {
        var clone = new SpreadsheetWorkbook
        {
            Sheets = Sheets.Select(s => s.Clone()).ToList(),
            ActiveSheetIndex = ActiveSheetIndex,
            NamedRanges = NamedRanges.Select(n => n.Clone()).ToList()
        };

        for (int i = 0; i < clone.Sheets.Count; i++)
        {
            clone.Sheets[i].Workbook = clone;
            clone.Sheets[i].SheetIndexInWorkbook = i;
        }

        return clone;
    }
}
