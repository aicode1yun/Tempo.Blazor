using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Adds a new sheet to the workbook. Supports undo.</summary>
public sealed class AddSheetCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetWorkbook _workbook;
    private readonly string _sheetName;
    private SpreadsheetSheet? _addedSheet;

    public AddSheetCommand(SpreadsheetWorkbook workbook, string sheetName)
    {
        _workbook = workbook;
        _sheetName = sheetName;
    }

    public void Execute()
    {
        _addedSheet = _workbook.AddSheet(_sheetName);
    }

    public void Undo()
    {
        if (_addedSheet is null) return;
        var index = _workbook.Sheets.IndexOf(_addedSheet);
        if (index >= 0)
        {
            _workbook.RemoveSheet(index);
        }
    }
}
