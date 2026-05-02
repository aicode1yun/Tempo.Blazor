using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Pastes clipboard content starting at the given target cell reference.</summary>
public sealed class PasteCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _targetCellRef;
    private readonly Dictionary<string, SpreadsheetCell?> _previousCells;

    public PasteCommand(SpreadsheetSheet sheet, string targetCellRef)
    {
        _sheet = sheet;
        _targetCellRef = targetCellRef;
        _previousCells = new Dictionary<string, SpreadsheetCell?>();
    }

    public void Execute()
    {
        if (SpreadsheetClipboard.Cells is null || SpreadsheetClipboard.Cells.Count == 0) return;

        var sourceRefs = SpreadsheetClipboard.Cells.Keys.ToList();
        var sourceStart = SpreadsheetRange.Parse(sourceRefs.Min()! + ":" + sourceRefs.Min()!);
        var targetStart = SpreadsheetRange.Parse(_targetCellRef + ":" + _targetCellRef);

        var dRow = targetStart.StartRow - sourceStart.StartRow;
        var dCol = targetStart.StartCol - sourceStart.StartCol;

        foreach (var kv in SpreadsheetClipboard.Cells)
        {
            var src = SpreadsheetRange.Parse(kv.Key + ":" + kv.Key);
            var destRow = src.StartRow + dRow;
            var destCol = src.StartCol + dCol;
            var destRef = $"{SpreadsheetRange.ColumnIndexToLetters(destCol)}{destRow + 1}";

            _previousCells[destRef] = _sheet.Cells.TryGetValue(destRef, out var existing) ? existing.Clone() : null;
            _sheet.Cells[destRef] = kv.Value.Clone();
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
