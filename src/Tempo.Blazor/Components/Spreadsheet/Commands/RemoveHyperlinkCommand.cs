using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Removes the hyperlink from a cell while preserving its value and formula. Supports undo.
/// </summary>
public sealed class RemoveHyperlinkCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _cellRef;

    private SpreadsheetHyperlink? _oldHyperlink;

    public RemoveHyperlinkCommand(SpreadsheetSheet sheet, string cellRef)
    {
        _sheet = sheet;
        _cellRef = cellRef;
    }

    public void Execute()
    {
        if (!_sheet.Cells.TryGetValue(_cellRef, out var cell))
            return;

        _oldHyperlink = cell.Hyperlink?.Clone();
        cell.Hyperlink = null;
    }

    public void Undo()
    {
        if (!_sheet.Cells.TryGetValue(_cellRef, out var cell))
            return;

        cell.Hyperlink = _oldHyperlink?.Clone();
    }
}
