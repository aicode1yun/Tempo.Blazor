using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Turns the auto-filter on over a range (or moves it to a new range). Enabling the filter adds the
/// header buttons but hides nothing until column criteria are set. Undo restores the previous filter
/// and the previous row visibility.
/// </summary>
public sealed class SetAutoFilterCommand : AutoFilterCommandBase, ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly SpreadsheetRange _range;

    private SpreadsheetAutoFilter? _oldFilter;
    private Dictionary<int, bool>? _oldHidden;

    /// <summary>Creates a command that enables an auto-filter over <paramref name="range"/>.</summary>
    public SetAutoFilterCommand(SpreadsheetSheet sheet, SpreadsheetRange range)
    {
        _sheet = sheet;
        _range = range;
    }

    public void Execute()
    {
        _oldFilter = _sheet.AutoFilter?.Clone();

        // Snapshot any rows that the previous filter may have hidden so undo restores them.
        if (_oldFilter is not null)
            _oldHidden = SnapshotHidden(_sheet, _oldFilter.FirstDataRow, _oldFilter.Range.EndRow);

        _sheet.AutoFilter = new SpreadsheetAutoFilter(
            new SpreadsheetRange(_range.StartRow, _range.StartCol, _range.EndRow, _range.EndCol));
    }

    public void Undo()
    {
        _sheet.AutoFilter = _oldFilter?.Clone();
        if (_oldHidden is not null)
            RestoreHidden(_sheet, _oldHidden);
    }
}
