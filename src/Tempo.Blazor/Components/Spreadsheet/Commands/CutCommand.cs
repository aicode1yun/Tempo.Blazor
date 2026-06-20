using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Cuts the current selection into the internal clipboard.</summary>
public sealed class CutCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<string> _cellRefs;
    private readonly Dictionary<string, SpreadsheetCell?> _previousCells;

    public CutCommand(SpreadsheetSheet sheet, IEnumerable<string> cellRefs)
    {
        _sheet = sheet;
        _cellRefs = cellRefs.ToList();
        _previousCells = new Dictionary<string, SpreadsheetCell?>();
    }

    public void Execute()
    {
        _previousCells.Clear();
        var cells = new Dictionary<string, SpreadsheetCell>();

        foreach (var r in _cellRefs)
        {
            _previousCells[r] = _sheet.Cells.TryGetValue(r, out var c) ? c.Clone() : null;
            cells[r] = _sheet.Cells.TryGetValue(r, out var existing) ? existing.Clone() : new SpreadsheetCell();
            _sheet.Cells.Remove(r);
        }

        var range = _cellRefs.Any()
            ? SpreadsheetRange.Parse(_cellRefs.Min()! + ":" + _cellRefs.Max()!)
            : new SpreadsheetRange(0, 0, 0, 0);

        SpreadsheetClipboard.Cut(cells, range.ToString());
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
