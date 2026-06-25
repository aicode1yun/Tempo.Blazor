using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Deletes a row at the specified index and shifts cells up. Supports undo.
/// </summary>
public sealed class DeleteRowCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _rowIndex;
    private readonly Dictionary<string, SpreadsheetCell> _deletedCells = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpreadsheetCell> _shiftedCells = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SpreadsheetRange> _oldMergedCells = [];

    public DeleteRowCommand(SpreadsheetSheet sheet, int rowIndex)
    {
        _sheet = sheet;
        _rowIndex = rowIndex;
    }

    public void Execute()
    {
        _deletedCells.Clear();
        _shiftedCells.Clear();
        _oldMergedCells.Clear();
        _oldMergedCells.AddRange(_sheet.MergedCells);

        // Capture deleted cells on the row
        var deleted = _sheet.Cells
            .Where(kv => ParseRow(kv.Key) == _rowIndex)
            .ToList();
        foreach (var kv in deleted)
        {
            _deletedCells[kv.Key] = kv.Value.Clone();
            _sheet.Cells.Remove(kv.Key);
        }

        // Capture and shift cells below
        var shifted = _sheet.Cells
            .Where(kv => ParseRow(kv.Key) > _rowIndex)
            .OrderBy(kv => ParseRow(kv.Key))
            .ToList();
        foreach (var kv in shifted)
        {
            _shiftedCells[kv.Key] = kv.Value.Clone();
            _sheet.Cells.Remove(kv.Key);
        }

        foreach (var kv in _shiftedCells)
        {
            var newRef = ShiftRow(kv.Key, -1);
            _sheet.Cells[newRef] = kv.Value.Clone();
        }

        // Update merged cells
        _sheet.MergedCells.Clear();
        foreach (var range in _oldMergedCells)
        {
            if (range.StartRow > _rowIndex)
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow - 1, range.StartCol, range.EndRow - 1, range.EndCol));
            }
            else if (range.EndRow >= _rowIndex)
            {
                // Partial overlap – shrink or remove
                if (range.StartRow == range.EndRow)
                    continue; // fully deleted
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow - 1, range.EndCol));
            }
            else
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow, range.EndCol));
            }
        }

        _sheet.RowCount--;
    }

    public void Undo()
    {
        // Remove shifted cells
        var cellsToRemove = _sheet.Cells
            .Where(kv => ParseRow(kv.Key) >= _rowIndex)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in cellsToRemove)
            _sheet.Cells.Remove(key);

        // Restore deleted cells
        foreach (var kv in _deletedCells)
        {
            _sheet.Cells[kv.Key] = kv.Value.Clone();
        }

        // Restore shifted cells to original positions
        foreach (var kv in _shiftedCells)
        {
            _sheet.Cells[kv.Key] = kv.Value.Clone();
        }

        // Restore merged cells
        _sheet.MergedCells.Clear();
        _sheet.MergedCells.AddRange(_oldMergedCells.Select(r => new SpreadsheetRange(r.StartRow, r.StartCol, r.EndRow, r.EndCol)));

        _sheet.RowCount++;
    }

    private static int ParseRow(string cellRef)
    {
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        return int.TryParse(numbers, out var row) ? row - 1 : 0;
    }

    private static string ShiftRow(string cellRef, int delta)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        var numbers = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        if (int.TryParse(numbers, out var row))
        {
            return $"{letters}{row + delta}";
        }
        return cellRef;
    }
}
