using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Removes duplicate rows from a range, keeping the first occurrence of each distinct key-column
/// combination and compacting the surviving rows upward within the range. Only the columns inside the
/// range are touched (Excel/OnlyOffice semantics); cells outside the range stay put. The whole
/// operation is a single atomic step: <see cref="Undo"/> restores the original layout exactly.
/// Ranges that intersect a merged cell are rejected.
/// </summary>
public sealed class RemoveDuplicatesCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly SpreadsheetRange _range;
    private readonly IReadOnlyList<int> _keyColumns;
    private readonly bool _hasHeader;
    private readonly bool _caseSensitive;
    private readonly CultureInfo _culture;

    private Dictionary<string, SpreadsheetCell?>? _snapshot;
    private readonly List<string> _affected = [];

    /// <summary>Creates the command for the given range and key columns (absolute column indices).</summary>
    public RemoveDuplicatesCommand(
        SpreadsheetSheet sheet,
        SpreadsheetRange range,
        IReadOnlyList<int> keyColumns,
        bool hasHeader,
        bool caseSensitive,
        CultureInfo culture)
    {
        _sheet = sheet;
        _range = range;
        _keyColumns = keyColumns;
        _hasHeader = hasHeader;
        _caseSensitive = caseSensitive;
        _culture = culture;
    }

    /// <summary>The number of duplicate rows removed by the last <see cref="Execute"/>.</summary>
    public int RemovedCount { get; private set; }

    /// <summary>The number of unique data rows that remained after the last <see cref="Execute"/>.</summary>
    public int RemainingCount { get; private set; }

    /// <summary>True when the range intersects a merged cell and therefore cannot be deduplicated.</summary>
    public bool HasMergeConflict => _sheet.MergedCells.Any(Intersects);

    /// <summary>The cell references touched by the last <see cref="Execute"/> (for renderer invalidation).</summary>
    public IReadOnlyList<string> AffectedCellRefs => _affected;

    public void Execute()
    {
        if (HasMergeConflict)
            return;

        var firstData = _hasHeader ? _range.StartRow + 1 : _range.StartRow;
        var removeSet = SpreadsheetDeduplicate
            .ComputeRowsToRemove(_sheet, _range, _keyColumns, _hasHeader, _caseSensitive, _culture)
            .ToHashSet();

        var keptRows = new List<int>();
        for (var row = firstData; row <= _range.EndRow; row++)
            if (!removeSet.Contains(row))
                keptRows.Add(row);

        RemovedCount = removeSet.Count;
        RemainingCount = keptRows.Count;

        _snapshot = SnapshotRegion(firstData);
        _affected.Clear();
        _affected.AddRange(_snapshot.Keys);

        if (removeSet.Count == 0)
            return; // nothing to do; snapshot kept so Undo is a no-op restore

        // Rebuild the compacted layout from the snapshot clones.
        var newCells = new Dictionary<string, SpreadsheetCell>(StringComparer.OrdinalIgnoreCase);
        for (var t = 0; t < keptRows.Count; t++)
        {
            var sourceRow = keptRows[t];
            var destRow = firstData + t;
            for (var col = _range.StartCol; col <= _range.EndCol; col++)
            {
                var sourceRef = CellRef(sourceRow, col);
                if (_snapshot.TryGetValue(sourceRef, out var original) && original is not null)
                    newCells[CellRef(destRow, col)] = original.Clone();
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
        foreach (var cellRef in _snapshot!.Keys)
            _sheet.Cells.Remove(cellRef);

        foreach (var (cellRef, cell) in newCells)
            _sheet.Cells[cellRef] = cell;

        Recalculate(_snapshot.Keys.Concat(newCells.Keys));
    }

    private Dictionary<string, SpreadsheetCell?> SnapshotRegion(int firstData)
    {
        var snapshot = new Dictionary<string, SpreadsheetCell?>(StringComparer.OrdinalIgnoreCase);
        for (var row = firstData; row <= _range.EndRow; row++)
            for (var col = _range.StartCol; col <= _range.EndCol; col++)
            {
                var cellRef = CellRef(row, col);
                snapshot[cellRef] = _sheet.Cells.TryGetValue(cellRef, out var cell) ? cell.Clone() : null;
            }

        return snapshot;
    }

    private void Recalculate(IEnumerable<string> cellRefs)
    {
        var refs = cellRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var cellRef in refs)
        {
            _sheet.UpdateDependencies(cellRef);
            if (_sheet.Cells.TryGetValue(cellRef, out var cell) && !string.IsNullOrEmpty(cell.Formula))
                _sheet.EvaluateFormula(cellRef);
        }

        foreach (var cellRef in refs)
            _sheet.RecalculateDependents(cellRef);
    }

    private static string CellRef(int row, int col)
        => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private bool Intersects(SpreadsheetRange merged)
        => merged.StartRow <= _range.EndRow && merged.EndRow >= _range.StartRow
        && merged.StartCol <= _range.EndCol && merged.EndCol >= _range.StartCol;
}
