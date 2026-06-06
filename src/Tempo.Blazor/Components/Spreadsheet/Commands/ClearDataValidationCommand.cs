using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Removes all data validation rules that overlap a given range and supports undo.
/// </summary>
public sealed class ClearDataValidationCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly SpreadsheetRange _range;

    // Snapshot of every rule removed during Execute
    private List<SpreadsheetDataValidation> _removed = new();

    public ClearDataValidationCommand(SpreadsheetSheet sheet, SpreadsheetRange range)
    {
        _sheet = sheet;
        _range = range;
    }

    public void Execute()
    {
        _removed = _sheet.DataValidations
            .Where(dv => RangesOverlap(dv.Range, _range))
            .Select(dv => dv with { })
            .ToList();

        foreach (var dv in _removed)
        {
            var stored = _sheet.DataValidations.FirstOrDefault(d => RangeSame(d.Range, dv.Range));
            if (stored is not null)
                _sheet.DataValidations.Remove(stored);
        }

        SetDataValidationCommand.ClearCellValidationRefs(_sheet, _range);
    }

    public void Undo()
    {
        foreach (var dv in _removed)
        {
            _sheet.DataValidations.Add(dv with { });
            SetDataValidationCommand.RefreshCellValidationRefs(_sheet, dv);
        }
    }

    private static bool RangeSame(SpreadsheetRange a, SpreadsheetRange b)
        => a.StartRow == b.StartRow && a.StartCol == b.StartCol
        && a.EndRow == b.EndRow && a.EndCol == b.EndCol;

    private static bool RangesOverlap(SpreadsheetRange a, SpreadsheetRange b)
        => a.StartRow <= b.EndRow && a.EndRow >= b.StartRow
        && a.StartCol <= b.EndCol && a.EndCol >= b.StartCol;
}
