using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Applies, replaces or clears the filter of a single column and recomputes which rows are hidden.
/// Pass a null or inactive <paramref name="columnFilter"/> to clear that column. Undo restores the
/// previous column filters and the previous row visibility.
/// </summary>
public sealed class UpdateColumnFilterCommand : AutoFilterCommandBase, ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _columnIndex;
    private readonly SpreadsheetColumnFilter? _columnFilter;
    private readonly CultureInfo _culture;

    private List<SpreadsheetColumnFilter>? _oldColumns;
    private Dictionary<int, bool>? _oldHidden;

    /// <summary>Creates a command that updates the filter for <paramref name="columnFilter"/>'s column.</summary>
    public UpdateColumnFilterCommand(SpreadsheetSheet sheet, SpreadsheetColumnFilter columnFilter, CultureInfo culture)
    {
        _sheet = sheet;
        _columnIndex = columnFilter.ColumnIndex;
        _columnFilter = columnFilter;
        _culture = culture;
    }

    /// <summary>Creates a command that clears the filter for <paramref name="columnIndex"/>.</summary>
    public UpdateColumnFilterCommand(SpreadsheetSheet sheet, int columnIndex, CultureInfo culture)
    {
        _sheet = sheet;
        _columnIndex = columnIndex;
        _columnFilter = null;
        _culture = culture;
    }

    public void Execute()
    {
        var filter = _sheet.AutoFilter;
        if (filter is null)
            return;

        _oldColumns = filter.Columns.Select(c => c.Clone()).ToList();
        _oldHidden = SnapshotHidden(_sheet, filter.FirstDataRow, filter.Range.EndRow);

        filter.Columns.RemoveAll(c => c.ColumnIndex == _columnIndex);
        if (_columnFilter is { IsActive: true })
            filter.Columns.Add(_columnFilter.Clone());

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(_sheet, filter, _culture);
        ApplyHidden(_sheet, filter.FirstDataRow, filter.Range.EndRow, [.. hidden]);
    }

    public void Undo()
    {
        var filter = _sheet.AutoFilter;
        if (filter is null || _oldColumns is null)
            return;

        filter.Columns = _oldColumns.Select(c => c.Clone()).ToList();
        if (_oldHidden is not null)
            RestoreHidden(_sheet, _oldHidden);
    }
}
