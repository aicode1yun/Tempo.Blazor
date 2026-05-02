using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Unmerges a range of cells and supports undo.
/// </summary>
public sealed class UnmergeCellsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _startRow;
    private readonly int _startCol;
    private readonly int _endRow;
    private readonly int _endCol;
    private SpreadsheetRange? _removedRange;

    public UnmergeCellsCommand(SpreadsheetSheet sheet, int startRow, int startCol, int endRow, int endCol)
    {
        _sheet = sheet;
        _startRow = startRow;
        _startCol = startCol;
        _endRow = endRow;
        _endCol = endCol;
    }

    public void Execute()
    {
        _removedRange = _sheet.MergedCells.FirstOrDefault(r =>
            r.StartRow == _startRow && r.StartCol == _startCol &&
            r.EndRow == _endRow && r.EndCol == _endCol);

        if (_removedRange is not null)
        {
            _sheet.MergedCells.Remove(_removedRange);
        }
    }

    public void Undo()
    {
        if (_removedRange is not null && !_sheet.MergedCells.Contains(_removedRange))
        {
            _sheet.MergedCells.Add(_removedRange);
        }
    }
}
