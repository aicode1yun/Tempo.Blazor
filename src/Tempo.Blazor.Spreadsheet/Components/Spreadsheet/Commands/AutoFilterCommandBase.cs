using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Shared helpers for auto-filter commands: snapshotting and restoring the hidden state of the data
/// rows in a filter range so undo restores row visibility exactly.
/// </summary>
public abstract class AutoFilterCommandBase
{
    /// <summary>Captures the <see cref="SpreadsheetRow.IsHidden"/> flag for every data row in the range.</summary>
    protected static Dictionary<int, bool> SnapshotHidden(SpreadsheetSheet sheet, int firstDataRow, int lastDataRow)
    {
        var snapshot = new Dictionary<int, bool>();
        for (var row = firstDataRow; row <= lastDataRow; row++)
            snapshot[row] = sheet.Rows.TryGetValue(row, out var r) && r.IsHidden;
        return snapshot;
    }

    /// <summary>Restores a previously captured hidden snapshot.</summary>
    protected static void RestoreHidden(SpreadsheetSheet sheet, Dictionary<int, bool> snapshot)
    {
        foreach (var (row, hidden) in snapshot)
            SetRowHidden(sheet, row, hidden);
    }

    /// <summary>Applies the given hidden row set across a range, clearing visibility for all other data rows.</summary>
    protected static void ApplyHidden(SpreadsheetSheet sheet, int firstDataRow, int lastDataRow, IReadOnlyCollection<int> hiddenRows)
    {
        var hidden = hiddenRows as HashSet<int> ?? [.. hiddenRows];
        for (var row = firstDataRow; row <= lastDataRow; row++)
            SetRowHidden(sheet, row, hidden.Contains(row));
    }

    /// <summary>Sets a row's hidden flag, creating row metadata if needed.</summary>
    protected static void SetRowHidden(SpreadsheetSheet sheet, int row, bool hidden)
    {
        if (!sheet.Rows.TryGetValue(row, out var meta))
        {
            if (!hidden)
                return; // nothing to create when leaving a row visible
            meta = new SpreadsheetRow { Index = row };
            sheet.Rows[row] = meta;
        }

        meta.IsHidden = hidden;
    }
}
