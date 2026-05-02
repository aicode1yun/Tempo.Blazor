using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Deletes a sheet from the workbook. Supports undo.</summary>
public sealed class DeleteSheetCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetWorkbook _workbook;
    private readonly int _sheetIndex;
    private SpreadsheetSheet? _deletedSheet;
    private int _previousActiveIndex;

    public DeleteSheetCommand(SpreadsheetWorkbook workbook, int sheetIndex)
    {
        _workbook = workbook;
        _sheetIndex = sheetIndex;
    }

    public void Execute()
    {
        if (_sheetIndex < 0 || _sheetIndex >= _workbook.Sheets.Count) return;

        _deletedSheet = _workbook.Sheets[_sheetIndex].Clone();
        _previousActiveIndex = _workbook.ActiveSheetIndex;

        _workbook.Sheets.RemoveAt(_sheetIndex);

        if (_workbook.ActiveSheetIndex >= _workbook.Sheets.Count)
            _workbook.ActiveSheetIndex = Math.Max(0, _workbook.Sheets.Count - 1);
    }

    public void Undo()
    {
        if (_deletedSheet is null) return;

        if (_sheetIndex >= _workbook.Sheets.Count)
        {
            _workbook.Sheets.Add(_deletedSheet.Clone());
        }
        else
        {
            _workbook.Sheets.Insert(_sheetIndex, _deletedSheet.Clone());
        }

        _workbook.ActiveSheetIndex = _previousActiveIndex;
    }
}
