using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Adds a named range to the workbook and recalculates any formulas that reference it.
/// Supports undo.
/// </summary>
public sealed class AddNamedRangeCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetWorkbook _workbook;
    private readonly SpreadsheetNamedRange _range;

    public AddNamedRangeCommand(SpreadsheetWorkbook workbook, SpreadsheetNamedRange range)
    {
        _workbook = workbook;
        _range = range;
    }

    public void Execute()
    {
        _workbook.NamedRanges.Add(_range);
        _workbook.RecalculateNamedRangeDependents(_range.Name);
    }

    public void Undo()
    {
        _workbook.NamedRanges.Remove(_range);
        _workbook.RecalculateNamedRangeDependents(_range.Name);
    }
}
