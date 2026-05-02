using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Deletes the content of the specified cells. Supports undo.</summary>
public sealed class DeleteCellsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<string> _cellRefs;
    private readonly Dictionary<string, SpreadsheetCell?> _previousCells;

    public DeleteCellsCommand(SpreadsheetSheet sheet, IEnumerable<string> cellRefs)
    {
        _sheet = sheet;
        _cellRefs = cellRefs.ToList();
        _previousCells = new Dictionary<string, SpreadsheetCell?>();
    }

    public void Execute()
    {
        foreach (var r in _cellRefs)
        {
            _previousCells[r] = _sheet.Cells.TryGetValue(r, out var c) ? c.Clone() : null;
            _sheet.Cells.Remove(r);
        }
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
