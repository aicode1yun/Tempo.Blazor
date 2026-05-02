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

    /// <summary>Creates a new workbook with a single default sheet.</summary>
    public SpreadsheetWorkbook()
    {
        AddSheet("Sheet1");
    }

    /// <summary>Adds a new sheet with the given name.</summary>
    public SpreadsheetSheet AddSheet(string name)
    {
        var sheet = new SpreadsheetSheet { Name = name };
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

    /// <summary>Creates a deep copy of this workbook including all sheets and cells.</summary>
    public SpreadsheetWorkbook Clone()
    {
        var clone = new SpreadsheetWorkbook
        {
            Sheets = Sheets.Select(s => s.Clone()).ToList(),
            ActiveSheetIndex = ActiveSheetIndex
        };
        return clone;
    }
}
