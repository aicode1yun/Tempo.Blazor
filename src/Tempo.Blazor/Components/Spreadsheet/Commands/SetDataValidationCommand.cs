using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Adds or replaces a data validation rule on a sheet range. Fully undoable: removes any
/// previous rule that overlapped the same range and restores it on undo.
/// </summary>
public sealed class SetDataValidationCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly SpreadsheetDataValidation _newRule;

    // Snapshot for undo: the previous rule at the same range (or null)
    private SpreadsheetDataValidation? _replaced;

    public SetDataValidationCommand(SpreadsheetSheet sheet, SpreadsheetDataValidation rule)
    {
        _sheet = sheet;
        _newRule = rule with { };
    }

    public void Execute()
    {
        // Remove any existing rule that covers exactly the same range
        _replaced = _sheet.DataValidations
            .FirstOrDefault(dv => RangeSame(dv.Range, _newRule.Range));

        if (_replaced is not null)
            _sheet.DataValidations.Remove(_replaced);

        _sheet.DataValidations.Add(_newRule with { });

        // Propagate the rule reference to every affected cell
        RefreshCellValidationRefs(_sheet, _newRule);
    }

    public void Undo()
    {
        // Remove the rule we added
        var added = _sheet.DataValidations
            .FirstOrDefault(dv => RangeSame(dv.Range, _newRule.Range));
        if (added is not null)
            _sheet.DataValidations.Remove(added);

        // Restore the previous rule (or clear cell refs if none)
        if (_replaced is not null)
        {
            _sheet.DataValidations.Add(_replaced with { });
            RefreshCellValidationRefs(_sheet, _replaced);
        }
        else
        {
            ClearCellValidationRefs(_sheet, _newRule.Range);
        }
    }

    private static bool RangeSame(SpreadsheetRange a, SpreadsheetRange b)
        => a.StartRow == b.StartRow && a.StartCol == b.StartCol
        && a.EndRow == b.EndRow && a.EndCol == b.EndCol;

    /// <summary>Updates <see cref="SpreadsheetCell.Validation"/> for every cell in the rule's range.</summary>
    internal static void RefreshCellValidationRefs(SpreadsheetSheet sheet, SpreadsheetDataValidation rule)
    {
        // Find the canonical rule object stored in the sheet list (so all cells share the same instance)
        var stored = sheet.DataValidations.FirstOrDefault(dv => RangeSame(dv.Range, rule.Range)) ?? rule;

        foreach (var cellRef in rule.Range.CellRefs)
        {
            var cell = sheet.GetOrCreateCell(cellRef);
            cell.Validation = stored;
        }
    }

    /// <summary>Clears <see cref="SpreadsheetCell.Validation"/> for every cell in the range.</summary>
    internal static void ClearCellValidationRefs(SpreadsheetSheet sheet, SpreadsheetRange range)
    {
        foreach (var cellRef in range.CellRefs)
        {
            if (sheet.Cells.TryGetValue(cellRef, out var cell))
                cell.Validation = null;
        }
    }
}
