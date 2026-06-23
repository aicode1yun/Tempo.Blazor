using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Deletes a column at the specified index and shifts cells left. Supports undo.
/// </summary>
public sealed class DeleteColumnCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _colIndex;
    private readonly Dictionary<string, SpreadsheetCell> _deletedCells = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpreadsheetCell> _shiftedCells = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SpreadsheetRange> _oldMergedCells = [];

    public DeleteColumnCommand(SpreadsheetSheet sheet, int colIndex)
    {
        _sheet = sheet;
        _colIndex = colIndex;
    }

    public void Execute()
    {
        _deletedCells.Clear();
        _shiftedCells.Clear();
        _oldMergedCells.Clear();
        _oldMergedCells.AddRange(_sheet.MergedCells);

        var deleted = _sheet.Cells
            .Where(kv => ParseCol(kv.Key) == _colIndex)
            .ToList();
        foreach (var kv in deleted)
        {
            _deletedCells[kv.Key] = kv.Value.Clone();
            _sheet.Cells.Remove(kv.Key);
        }

        var shifted = _sheet.Cells
            .Where(kv => ParseCol(kv.Key) > _colIndex)
            .OrderBy(kv => ParseCol(kv.Key))
            .ToList();
        foreach (var kv in shifted)
        {
            _shiftedCells[kv.Key] = kv.Value.Clone();
            _sheet.Cells.Remove(kv.Key);
        }

        foreach (var kv in _shiftedCells)
        {
            var newRef = ShiftCol(kv.Key, -1);
            _sheet.Cells[newRef] = kv.Value.Clone();
        }

        _sheet.MergedCells.Clear();
        foreach (var range in _oldMergedCells)
        {
            if (range.StartCol > _colIndex)
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol - 1, range.EndRow, range.EndCol - 1));
            }
            else if (range.EndCol >= _colIndex)
            {
                if (range.StartCol == range.EndCol)
                    continue;
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow, range.EndCol - 1));
            }
            else
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow, range.EndCol));
            }
        }

        _sheet.ColumnCount--;
    }

    public void Undo()
    {
        var cellsToRemove = _sheet.Cells
            .Where(kv => ParseCol(kv.Key) >= _colIndex)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in cellsToRemove)
            _sheet.Cells.Remove(key);

        foreach (var kv in _deletedCells)
        {
            _sheet.Cells[kv.Key] = kv.Value.Clone();
        }

        foreach (var kv in _shiftedCells)
        {
            _sheet.Cells[kv.Key] = kv.Value.Clone();
        }

        _sheet.MergedCells.Clear();
        _sheet.MergedCells.AddRange(_oldMergedCells.Select(r => new SpreadsheetRange(r.StartRow, r.StartCol, r.EndRow, r.EndCol)));

        _sheet.ColumnCount++;
    }

    private static int ParseCol(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        return SpreadsheetRange.ColumnLettersToIndex(letters);
    }

    private static string ShiftCol(string cellRef, int delta)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = SpreadsheetRange.ColumnLettersToIndex(letters);
        var newLetters = SpreadsheetRange.ColumnIndexToLetters(col + delta);
        return $"{newLetters}{numbers}";
    }
}
