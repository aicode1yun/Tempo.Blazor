using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Sets the width of a column and supports undo.</summary>
public sealed class ResizeColumnCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _columnIndex;
    private readonly double _newWidth;
    private double? _oldWidth;

    public ResizeColumnCommand(SpreadsheetSheet sheet, int columnIndex, double newWidth)
    {
        _sheet = sheet;
        _columnIndex = columnIndex;
        _newWidth = newWidth;
    }

    public void Execute()
    {
        _oldWidth = _sheet.Columns.TryGetValue(_columnIndex, out var existing) ? existing.Width : null;

        if (!_sheet.Columns.TryGetValue(_columnIndex, out var col))
        {
            col = new SpreadsheetColumn { Index = _columnIndex };
            _sheet.Columns[_columnIndex] = col;
        }
        col.Width = _newWidth;
    }

    public void Undo()
    {
        if (!_sheet.Columns.TryGetValue(_columnIndex, out var col))
        {
            if (_oldWidth is null) return;
            col = new SpreadsheetColumn { Index = _columnIndex };
            _sheet.Columns[_columnIndex] = col;
        }
        col.Width = _oldWidth;
    }
}
