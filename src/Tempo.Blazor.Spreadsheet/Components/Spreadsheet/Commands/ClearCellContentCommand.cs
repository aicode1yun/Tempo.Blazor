using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Clears the value and formula of selected cells while preserving their style. Supports undo.</summary>
public sealed class ClearCellContentCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<string> _cellRefs;
    private readonly Dictionary<string, (object? Value, string? Formula)> _previousContent = new(StringComparer.OrdinalIgnoreCase);

    public ClearCellContentCommand(SpreadsheetSheet sheet, IEnumerable<string> cellRefs)
    {
        _sheet = sheet;
        _cellRefs = cellRefs.ToList();
    }

    public void Execute()
    {
        _previousContent.Clear();
        foreach (var cellRef in _cellRefs)
        {
            if (_sheet.Cells.TryGetValue(cellRef, out var cell))
            {
                _previousContent[cellRef] = (cell.Value, cell.Formula);
                cell.Value = null;
                cell.Formula = null;
                cell.DisplayValue = null;
            }
        }
    }

    public void Undo()
    {
        foreach (var (cellRef, (value, formula)) in _previousContent)
        {
            var cell = _sheet.GetOrCreateCell(cellRef);
            cell.Value = value;
            cell.Formula = formula;
            cell.DisplayValue = null;
        }
    }
}
