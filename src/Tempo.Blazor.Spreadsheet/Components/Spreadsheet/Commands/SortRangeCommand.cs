using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Sorts a range by reordering whole rows (or columns when <see cref="SpreadsheetSortSpec.ByRows"/>
/// is set). Cell values, formulas and styles move with their row; relative formula references are
/// shifted by the row delta of the move so they keep pointing at the same logical data. Undo restores
/// the original cell layout exactly. Ranges that intersect merged cells are rejected.
/// </summary>
public sealed class SortRangeCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly SpreadsheetSortSpec _spec;
    private readonly CultureInfo _culture;

    private Dictionary<string, SpreadsheetCell?>? _snapshot;
    private readonly List<string> _affected = [];

    /// <summary>Creates a sort command for the given specification.</summary>
    public SortRangeCommand(SpreadsheetSheet sheet, SpreadsheetSortSpec spec, CultureInfo culture)
    {
        _sheet = sheet;
        _spec = spec;
        _culture = culture;
    }

    /// <summary>True when the range intersects a merged cell and therefore cannot be sorted.</summary>
    public bool HasMergeConflict => _sheet.MergedCells.Any(Intersects);

    /// <summary>The cell references touched by the last <see cref="Execute"/> (for renderer invalidation).</summary>
    public IReadOnlyList<string> AffectedCellRefs => _affected;

    public void Execute()
    {
        if (HasMergeConflict)
            return;

        var order = SpreadsheetSortEngine.ComputeOrder(_sheet, _spec, _culture);
        var firstData = FirstDataIndex();

        // Snapshot every cell in the data region so undo can restore it.
        _snapshot = SnapshotRegion();
        _affected.Clear();
        _affected.AddRange(_snapshot.Keys);

        // Build the destination layout from the snapshot (source clones), shifting formulas.
        var newCells = new Dictionary<string, SpreadsheetCell>(StringComparer.OrdinalIgnoreCase);
        for (var t = 0; t < order.Count; t++)
        {
            var source = order[t];
            var dest = firstData + t;
            var delta = dest - source;

            foreach (var cross in CrossAxisIndices())
            {
                var sourceRef = CellRef(source, cross);
                if (!_snapshot.TryGetValue(sourceRef, out var original) || original is null)
                    continue;

                var moved = original.Clone();
                if (!string.IsNullOrEmpty(moved.Formula))
                {
                    moved.Formula = _spec.ByRows
                        ? FormulaReferenceAdjuster.AdjustFormula(moved.Formula!, 0, delta)
                        : FormulaReferenceAdjuster.AdjustFormula(moved.Formula!, delta, 0);
                }

                newCells[CellRef(dest, cross)] = moved;
            }
        }

        ApplyLayout(newCells);
    }

    public void Undo()
    {
        if (_snapshot is null)
            return;

        foreach (var (cellRef, cell) in _snapshot)
        {
            if (cell is null)
                _sheet.Cells.Remove(cellRef);
            else
                _sheet.Cells[cellRef] = cell.Clone();
        }

        Recalculate(_snapshot.Keys);
    }

    private void ApplyLayout(Dictionary<string, SpreadsheetCell> newCells)
    {
        // Clear the whole data region first, then place the moved cells.
        foreach (var cellRef in _snapshot!.Keys)
            _sheet.Cells.Remove(cellRef);

        foreach (var (cellRef, cell) in newCells)
            _sheet.Cells[cellRef] = cell;

        Recalculate(_snapshot.Keys.Concat(newCells.Keys));
    }

    private void Recalculate(IEnumerable<string> cellRefs)
    {
        var refs = cellRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var cellRef in refs)
        {
            if (_sheet.Cells.TryGetValue(cellRef, out var cell) && !string.IsNullOrEmpty(cell.Formula))
            {
                _sheet.UpdateDependencies(cellRef);
                _sheet.EvaluateFormula(cellRef);
            }
            else
            {
                _sheet.UpdateDependencies(cellRef);
            }
        }

        foreach (var cellRef in refs)
            _sheet.RecalculateDependents(cellRef);
    }

    private Dictionary<string, SpreadsheetCell?> SnapshotRegion()
    {
        var snapshot = new Dictionary<string, SpreadsheetCell?>(StringComparer.OrdinalIgnoreCase);
        var firstData = FirstDataIndex();
        var lastPrimary = _spec.ByRows ? _spec.Range.EndCol : _spec.Range.EndRow;

        for (var primary = firstData; primary <= lastPrimary; primary++)
            foreach (var cross in CrossAxisIndices())
            {
                var cellRef = CellRef(primary, cross);
                snapshot[cellRef] = _sheet.Cells.TryGetValue(cellRef, out var cell) ? cell.Clone() : null;
            }

        return snapshot;
    }

    private int FirstDataIndex()
    {
        var start = _spec.ByRows ? _spec.Range.StartCol : _spec.Range.StartRow;
        return _spec.HasHeader ? start + 1 : start;
    }

    private IEnumerable<int> CrossAxisIndices()
    {
        var (from, to) = _spec.ByRows
            ? (_spec.Range.StartRow, _spec.Range.EndRow)
            : (_spec.Range.StartCol, _spec.Range.EndCol);
        for (var i = from; i <= to; i++)
            yield return i;
    }

    private string CellRef(int primary, int cross)
    {
        var (row, col) = _spec.ByRows ? (cross, primary) : (primary, cross);
        return $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
    }

    private bool Intersects(SpreadsheetRange merged)
        => merged.StartRow <= _spec.Range.EndRow && merged.EndRow >= _spec.Range.StartRow
        && merged.StartCol <= _spec.Range.EndCol && merged.EndCol >= _spec.Range.StartCol;
}
