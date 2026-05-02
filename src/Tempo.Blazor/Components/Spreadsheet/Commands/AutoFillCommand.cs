using Tempo.Blazor.Components.Spreadsheet.AutoFill;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Undoable command that performs an AutoFill series expansion.</summary>
public sealed class AutoFillCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _sourceRange;
    private readonly string _targetRange;
    private readonly Dictionary<string, SpreadsheetCell?> _previousCells;

    public AutoFillCommand(SpreadsheetSheet sheet, string sourceRange, string targetRange)
    {
        _sheet = sheet;
        _sourceRange = sourceRange;
        _targetRange = targetRange;
        _previousCells = new Dictionary<string, SpreadsheetCell?>();
    }

    public void Execute()
    {
        // Store previous values for undo
        var target = SpreadsheetRange.Parse(_targetRange);
        foreach (var cellRef in target.CellRefs)
        {
            _previousCells[cellRef] = _sheet.Cells.TryGetValue(cellRef, out var cell) ? cell.Clone() : null;
        }

        var engine = new SpreadsheetAutoFillEngine(_sheet);
        engine.Fill(_sourceRange, _targetRange);
    }

    public void Undo()
    {
        foreach (var kv in _previousCells)
        {
            if (kv.Value is null)
            {
                _sheet.Cells.Remove(kv.Key);
            }
            else
            {
                _sheet.Cells[kv.Key] = kv.Value;
            }
        }
    }
}
