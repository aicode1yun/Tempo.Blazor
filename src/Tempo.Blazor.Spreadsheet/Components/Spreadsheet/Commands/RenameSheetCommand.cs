using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Renames a sheet in the workbook. Supports undo.</summary>
public sealed class RenameSheetCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _newName;
    private string? _previousName;

    public RenameSheetCommand(SpreadsheetSheet sheet, string newName)
    {
        _sheet = sheet;
        _newName = newName;
    }

    public void Execute()
    {
        _previousName = _sheet.Name;
        _sheet.Name = _newName;
    }

    public void Undo()
    {
        if (_previousName is not null)
            _sheet.Name = _previousName;
    }
}
