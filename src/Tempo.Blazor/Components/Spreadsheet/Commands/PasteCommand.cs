using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Pastes clipboard content starting at the given target cell reference.</summary>
public sealed class PasteCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _targetCellRef;
    private readonly Dictionary<string, SpreadsheetCell?> _previousCells;
    private readonly List<string> _affectedCellRefs = [];

    /// <summary>Cell references changed by the latest paste execution.</summary>
    public IReadOnlyList<string> AffectedCellRefs => _affectedCellRefs;

    public PasteCommand(SpreadsheetSheet sheet, string targetCellRef)
    {
        _sheet = sheet;
        _targetCellRef = targetCellRef;
        _previousCells = new Dictionary<string, SpreadsheetCell?>();
    }

    public void Execute()
    {
        _affectedCellRefs.Clear();
        if (SpreadsheetClipboard.Cells is null || SpreadsheetClipboard.Cells.Count == 0) return;

        var sourceRefs = SpreadsheetClipboard.Cells.Keys.ToList();
        var sourceStart = SpreadsheetRange.Parse(sourceRefs.Min()! + ":" + sourceRefs.Min()!);
        var targetStart = SpreadsheetRange.Parse(_targetCellRef + ":" + _targetCellRef);

        var dRow = targetStart.StartRow - sourceStart.StartRow;
        var dCol = targetStart.StartCol - sourceStart.StartCol;

        var destRefs = new List<string>();
        foreach (var kv in SpreadsheetClipboard.Cells)
        {
            var src = SpreadsheetRange.Parse(kv.Key + ":" + kv.Key);
            var destRow = src.StartRow + dRow;
            var destCol = src.StartCol + dCol;
            var destRef = $"{SpreadsheetRange.ColumnIndexToLetters(destCol)}{destRow + 1}";

            _previousCells[destRef] = _sheet.Cells.TryGetValue(destRef, out var existing) ? existing.Clone() : null;

            var clonedCell = kv.Value.Clone();
            // For copy (not cut): adjust relative formula references by the paste offset
            if (!SpreadsheetClipboard.IsCut && clonedCell.Formula is not null)
            {
                clonedCell.Formula = FormulaReferenceAdjuster.AdjustFormula(clonedCell.Formula, dRow, dCol);
                clonedCell.Value = null;
                clonedCell.DisplayValue = null;
            }

            _sheet.Cells[destRef] = clonedCell;
            destRefs.Add(destRef);
            _affectedCellRefs.Add(destRef);
        }

        // Update dependency graph and evaluate adjusted formulas
        foreach (var destRef in destRefs)
        {
            if (_sheet.Cells.TryGetValue(destRef, out var cell) && cell.Formula is not null)
            {
                _sheet.UpdateDependencies(destRef);
                _sheet.EvaluateFormula(destRef);
            }
        }

        if (SpreadsheetClipboard.IsCut)
            SpreadsheetClipboard.Clear();
    }

    public void Undo()
    {
        foreach (var kv in _previousCells)
        {
            if (kv.Value is null)
                _sheet.Cells.Remove(kv.Key);
            else
                _sheet.Cells[kv.Key] = kv.Value;
        }
    }
}
