using System.Text.RegularExpressions;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Inserts a row at the specified index and shifts cells down. Supports undo.
/// </summary>
public sealed class InsertRowCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _rowIndex;
    private readonly Dictionary<string, SpreadsheetCell> _shiftedCells = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SpreadsheetRange> _oldMergedCells = [];

    public InsertRowCommand(SpreadsheetSheet sheet, int rowIndex)
    {
        _sheet = sheet;
        _rowIndex = rowIndex;
    }

    public void Execute()
    {
        _shiftedCells.Clear();
        _oldMergedCells.Clear();
        _oldMergedCells.AddRange(_sheet.MergedCells);

        var cellsToShift = _sheet.Cells
            .Where(kv => ParseRow(kv.Key) >= _rowIndex)
            .OrderByDescending(kv => ParseRow(kv.Key))
            .ToList();

        foreach (var kv in cellsToShift)
        {
            _shiftedCells[kv.Key] = kv.Value.Clone();
            _sheet.Cells.Remove(kv.Key);
        }

        foreach (var kv in _shiftedCells.OrderBy(kv => ParseRow(kv.Key)))
        {
            var newRef = ShiftRow(kv.Key, 1);
            _sheet.Cells[newRef] = kv.Value.Clone();
        }

        // Update merged cells
        _sheet.MergedCells.Clear();
        foreach (var range in _oldMergedCells)
        {
            if (range.StartRow >= _rowIndex)
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow + 1, range.StartCol, range.EndRow + 1, range.EndCol));
            }
            else if (range.EndRow >= _rowIndex)
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow + 1, range.EndCol));
            }
            else
            {
                _sheet.MergedCells.Add(new SpreadsheetRange(range.StartRow, range.StartCol, range.EndRow, range.EndCol));
            }
        }

        _sheet.RowCount++;
    }

    public void Undo()
    {
        // Remove shifted cells
        var cellsToRemove = _sheet.Cells
            .Where(kv => ParseRow(kv.Key) > _rowIndex)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in cellsToRemove)
            _sheet.Cells.Remove(key);

        // Restore original cells
        foreach (var kv in _shiftedCells)
        {
            _sheet.Cells[kv.Key] = kv.Value.Clone();
        }

        // Restore merged cells
        _sheet.MergedCells.Clear();
        _sheet.MergedCells.AddRange(_oldMergedCells.Select(r => new SpreadsheetRange(r.StartRow, r.StartCol, r.EndRow, r.EndCol)));

        _sheet.RowCount--;
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
