using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Merges a range of cells and supports undo.
/// </summary>
public sealed class MergeCellsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _startRow;
    private readonly int _startCol;
    private readonly int _endRow;
    private readonly int _endCol;
    private bool _wasAlreadyMerged;

    public MergeCellsCommand(SpreadsheetSheet sheet, int startRow, int startCol, int endRow, int endCol)
    {
        _sheet = sheet;
        _startRow = startRow;
        _startCol = startCol;
        _endRow = endRow;
        _endCol = endCol;
    }

    public void Execute()
    {
        var range = new SpreadsheetRange(_startRow, _startCol, _endRow, _endCol);
        _wasAlreadyMerged = _sheet.MergedCells.Any(r =>
            r.StartRow == range.StartRow && r.StartCol == range.StartCol &&
            r.EndRow == range.EndRow && r.EndCol == range.EndCol);

        if (!_wasAlreadyMerged)
        {
            _sheet.MergedCells.Add(range);
        }
    }

    public void Undo()
    {
        if (!_wasAlreadyMerged)
        {
            _sheet.MergedCells.RemoveAll(r =>
                r.StartRow == _startRow && r.StartCol == _startCol &&
                r.EndRow == _endRow && r.EndCol == _endCol);
        }
    }
}
