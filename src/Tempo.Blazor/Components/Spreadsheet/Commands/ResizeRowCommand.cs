using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Sets the height of a row and supports undo.</summary>
public sealed class ResizeRowCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _rowIndex;
    private readonly double _newHeight;
    private double? _oldHeight;

    public ResizeRowCommand(SpreadsheetSheet sheet, int rowIndex, double newHeight)
    {
        _sheet = sheet;
        _rowIndex = rowIndex;
        _newHeight = newHeight;
    }

    public void Execute()
    {
        _oldHeight = _sheet.Rows.TryGetValue(_rowIndex, out var existing) ? existing.Height : null;

        if (!_sheet.Rows.TryGetValue(_rowIndex, out var row))
        {
            row = new SpreadsheetRow { Index = _rowIndex };
            _sheet.Rows[_rowIndex] = row;
        }
        row.Height = _newHeight;
    }

    public void Undo()
    {
        if (!_sheet.Rows.TryGetValue(_rowIndex, out var row))
        {
            if (_oldHeight is null) return;
            row = new SpreadsheetRow { Index = _rowIndex };
            _sheet.Rows[_rowIndex] = row;
        }
        row.Height = _oldHeight;
    }
}
