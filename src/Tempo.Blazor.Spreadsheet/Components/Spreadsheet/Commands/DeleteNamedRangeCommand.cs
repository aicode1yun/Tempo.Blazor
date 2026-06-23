using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Deletes a named range from the workbook and recalculates any formulas that referenced it.
/// Supports undo.
/// </summary>
public sealed class DeleteNamedRangeCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetWorkbook _workbook;
    private readonly SpreadsheetNamedRange _range;
    private readonly int _originalIndex;

    public DeleteNamedRangeCommand(SpreadsheetWorkbook workbook, SpreadsheetNamedRange range)
    {
        _workbook = workbook;
        _range = range;
        _originalIndex = workbook.NamedRanges.IndexOf(range);
    }

    public void Execute()
    {
        _workbook.NamedRanges.Remove(_range);
        _workbook.RecalculateNamedRangeDependents(_range.Name);
    }

    public void Undo()
    {
        if (_originalIndex >= 0 && _originalIndex <= _workbook.NamedRanges.Count)
            _workbook.NamedRanges.Insert(_originalIndex, _range);
        else
            _workbook.NamedRanges.Add(_range);

        _workbook.RecalculateNamedRangeDependents(_range.Name);
    }
}
