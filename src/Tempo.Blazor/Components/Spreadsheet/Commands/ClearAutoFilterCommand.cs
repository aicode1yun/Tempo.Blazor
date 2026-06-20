using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Removes the sheet's auto-filter and reveals every row it had hidden. Undo restores the filter and
/// the previous row visibility exactly.
/// </summary>
public sealed class ClearAutoFilterCommand : AutoFilterCommandBase, ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;

    private SpreadsheetAutoFilter? _oldFilter;
    private Dictionary<int, bool>? _oldHidden;

    /// <summary>Creates a command that clears the sheet's auto-filter.</summary>
    public ClearAutoFilterCommand(SpreadsheetSheet sheet)
    {
        _sheet = sheet;
    }

    public void Execute()
    {
        _oldFilter = _sheet.AutoFilter?.Clone();
        if (_oldFilter is null)
            return;

        _oldHidden = SnapshotHidden(_sheet, _oldFilter.FirstDataRow, _oldFilter.Range.EndRow);

        // Reveal all data rows in the former filter range.
        ApplyHidden(_sheet, _oldFilter.FirstDataRow, _oldFilter.Range.EndRow, []);
        _sheet.AutoFilter = null;
    }

    public void Undo()
    {
        _sheet.AutoFilter = _oldFilter?.Clone();
        if (_oldHidden is not null)
            RestoreHidden(_sheet, _oldHidden);
    }
}
