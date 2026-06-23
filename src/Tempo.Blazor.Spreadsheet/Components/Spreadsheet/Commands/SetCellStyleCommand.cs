using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Applies a style mutation to multiple cells and supports undo.
/// </summary>
public sealed class SetCellStyleCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<string> _cellRefs;
    private readonly Action<SpreadsheetCellStyle> _mutate;
    private readonly Dictionary<string, SpreadsheetCellStyle> _oldStyles = new(StringComparer.OrdinalIgnoreCase);

    public SetCellStyleCommand(SpreadsheetSheet sheet, IEnumerable<string> cellRefs, Action<SpreadsheetCellStyle> mutate)
    {
        _sheet = sheet;
        _cellRefs = cellRefs.ToList();
        _mutate = mutate;
    }

    public void Execute()
    {
        _oldStyles.Clear();
        foreach (var cellRef in _cellRefs)
        {
            var cell = _sheet.GetOrCreateCell(cellRef);
            _oldStyles[cellRef] = cell.Style.Clone();
            cell.Style = cell.Style.Clone();
            _mutate(cell.Style);
        }
    }

    public void Undo()
    {
        foreach (var cellRef in _cellRefs)
        {
            if (_oldStyles.TryGetValue(cellRef, out var oldStyle))
            {
                var cell = _sheet.GetOrCreateCell(cellRef);
                cell.Style = oldStyle.Clone();
            }
        }
    }
}
