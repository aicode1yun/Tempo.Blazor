using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Copies the current selection into the internal clipboard.</summary>
public sealed class CopyCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly IEnumerable<string> _cellRefs;

    public CopyCommand(SpreadsheetSheet sheet, IEnumerable<string> cellRefs)
    {
        _sheet = sheet;
        _cellRefs = cellRefs;
    }

    public void Execute()
    {
        var cells = _cellRefs.ToDictionary(
            r => r,
            r => _sheet.Cells.TryGetValue(r, out var c) ? c.Clone() : new SpreadsheetCell());

        var range = _cellRefs.Any()
            ? SpreadsheetRange.Parse(_cellRefs.Min()! + ":" + _cellRefs.Max()!)
            : new SpreadsheetRange(0, 0, 0, 0);

        SpreadsheetClipboard.Copy(cells, range.ToString());
    }

    public void Undo()
    {
        // Copy is not undoable; clipboard state is preserved
    }
}
