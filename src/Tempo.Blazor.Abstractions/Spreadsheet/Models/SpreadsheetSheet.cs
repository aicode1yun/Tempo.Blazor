using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Formula;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents a single worksheet within a spreadsheet workbook.
/// </summary>
public sealed class SpreadsheetSheet
{
    private FormulaEngine? _engine;
    private readonly Dictionary<string, HashSet<string>> _dependents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The name displayed on the sheet tab.</summary>
    public string Name { get; set; } = "Sheet1";

    /// <summary>All cells in the sheet keyed by A1 reference (e.g. A1, B2).</summary>
    public Dictionary<string, SpreadsheetCell> Cells { get; set; } = new();

    /// <summary>Row metadata keyed by zero-based row index.</summary>
    public Dictionary<int, SpreadsheetRow> Rows { get; set; } = new();

    /// <summary>Column metadata keyed by zero-based column index.</summary>
    public Dictionary<int, SpreadsheetColumn> Columns { get; set; } = new();

    /// <summary>All merged cell ranges in this sheet.</summary>
    public List<SpreadsheetRange> MergedCells { get; set; } = new();

    /// <summary>The total number of rows in the sheet.</summary>
    public int RowCount { get; set; } = 200;

    /// <summary>The total number of columns in the sheet.</summary>
    public int ColumnCount { get; set; } = 50;

    /// <summary>The default row height in pixels.</summary>
    public double DefaultRowHeight { get; set; } = 20;

    /// <summary>The default column width in pixels.</summary>
    public double DefaultColumnWidth { get; set; } = 64;

    /// <summary>Whether grid lines are visible.</summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>The A1 reference of the currently active (selected) cell.</summary>
    public string? ActiveCellRef { get; set; }

    /// <summary>The number of rows to freeze at the top (0 = none).</summary>
    public int FreezeRowCount { get; set; }

    /// <summary>The number of columns to freeze on the left (0 = none).</summary>
    public int FreezeColumnCount { get; set; }

    /// <summary>The auto-filter applied to this sheet, or null when none is active.</summary>
    public SpreadsheetAutoFilter? AutoFilter { get; set; }

    /// <summary>Data validation rules defined on this sheet.</summary>
    public List<SpreadsheetDataValidation> DataValidations { get; set; } = new();

    /// <summary>Retrieves or creates a cell at the given A1 reference.</summary>
    public SpreadsheetCell GetOrCreateCell(string cellRef)
    {
        if (Cells.TryGetValue(cellRef, out var cell))
            return cell;

        cell = new SpreadsheetCell();
        Cells[cellRef] = cell;
        return cell;
    }

    /// <summary>Retrieves a cell at the given row and column indices.</summary>
    public SpreadsheetCell? GetCell(int row, int col)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        return Cells.TryGetValue(cellRef, out var cell) ? cell : null;
    }

    /// <summary>Sets a cell value at the given row and column indices and recalculates dependents.</summary>
    public void SetCellValue(int row, int col, object? value)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        var cell = GetOrCreateCell(cellRef);
        ClearDependencies(cellRef);
        cell.Value = value;
        cell.Formula = null;
        cell.DisplayValue = null;
        RecalculateDependents(cellRef);
    }

    /// <summary>Sets a cell formula at the given row and column indices, evaluates it and recalculates dependents.</summary>
    public void SetCellFormula(int row, int col, string formula)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        var cell = GetOrCreateCell(cellRef);
        cell.Formula = formula;
        UpdateDependencies(cellRef);
        EvaluateFormula(cellRef);
        RecalculateDependents(cellRef);
    }

    /// <summary>Evaluates the formula of a specific cell and stores the result in Value/DisplayValue.</summary>
    public void EvaluateFormula(string cellRef)
    {
        if (!Cells.TryGetValue(cellRef, out var cell)) return;
        if (string.IsNullOrEmpty(cell.Formula)) return;

        try
        {
            _engine ??= new FormulaEngine();
            var result = _engine.Evaluate(cell.Formula, this);
            cell.Value = result;
            cell.DisplayValue = null; // Let the grid formatter handle display formatting
        }
        catch
        {
            cell.Value = "#ERROR";
            cell.DisplayValue = null;
        }
    }

    /// <summary>Updates the dependency graph for the given cell after its formula changes.</summary>
    public void UpdateDependencies(string cellRef)
    {
        ClearDependencies(cellRef);
        if (!Cells.TryGetValue(cellRef, out var cell)) return;
        if (string.IsNullOrEmpty(cell.Formula)) return;

        var refs = FormulaDependencyExtractor.ExtractCellRefs(cell.Formula);
        foreach (var referencedRef in refs)
        {
            if (!_dependents.TryGetValue(referencedRef, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _dependents[referencedRef] = set;
            }
            set.Add(cellRef);
        }
    }

    private void ClearDependencies(string cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef))
            return;

        foreach (var entry in _dependents.ToArray())
        {
            if (!entry.Value.Remove(cellRef))
                continue;

            if (entry.Value.Count == 0)
                _dependents.Remove(entry.Key);
        }
    }

    /// <summary>Recursively recalculates all cells that depend on the given cell.</summary>
    public void RecalculateDependents(string cellRef)
    {
        RecalculateDependents(cellRef, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the given cells plus every transitive dependent cell that may need
    /// a renderer refresh after recalculation.
    /// </summary>
    public IReadOnlyList<string> GetCellAndDependentRefs(IEnumerable<string> cellRefs)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cellRef in cellRefs)
        {
            if (!string.IsNullOrWhiteSpace(cellRef))
                CollectCellAndDependents(cellRef, visited);
        }

        return visited.ToArray();
    }

    private void RecalculateDependents(string cellRef, HashSet<string> visited)
    {
        if (!visited.Add(cellRef)) return; // prevent infinite recursion on circular references
        if (!_dependents.TryGetValue(cellRef, out var dependents)) return;

        foreach (var dependentRef in dependents.ToList())
        {
            EvaluateFormula(dependentRef);
            RecalculateDependents(dependentRef, visited);
        }
    }

    private void CollectCellAndDependents(string cellRef, HashSet<string> visited)
    {
        if (!visited.Add(cellRef))
            return;

        if (!_dependents.TryGetValue(cellRef, out var dependents))
            return;

        foreach (var dependentRef in dependents)
            CollectCellAndDependents(dependentRef, visited);
    }

    /// <summary>Creates a deep copy of this sheet.</summary>
    public SpreadsheetSheet Clone()
    {
        var clone = new SpreadsheetSheet
        {
            Name = Name,
            RowCount = RowCount,
            ColumnCount = ColumnCount,
            DefaultRowHeight = DefaultRowHeight,
            DefaultColumnWidth = DefaultColumnWidth,
            FreezeRowCount = FreezeRowCount,
            FreezeColumnCount = FreezeColumnCount,
            Rows = Rows.ToDictionary(r => r.Key, r => new SpreadsheetRow { Index = r.Value.Index, Height = r.Value.Height, IsHidden = r.Value.IsHidden }),
            Columns = Columns.ToDictionary(c => c.Key, c => new SpreadsheetColumn { Index = c.Value.Index, Width = c.Value.Width, IsHidden = c.Value.IsHidden }),
            MergedCells = MergedCells.Select(m => new SpreadsheetRange(m.StartRow, m.StartCol, m.EndRow, m.EndCol)).ToList(),
            AutoFilter = AutoFilter?.Clone(),
            DataValidations = DataValidations.Select(dv => dv.DeepClone()).ToList()
        };

        foreach (var kv in Cells)
        {
            clone.Cells[kv.Key] = kv.Value.Clone();
        }

        return clone;
    }
}
