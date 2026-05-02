using System.Globalization;
using System.Text.RegularExpressions;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.AutoFill;

/// <summary>
/// Detects patterns in a source range and fills them into a target range.
/// </summary>
public sealed class SpreadsheetAutoFillEngine
{
    private readonly SpreadsheetSheet _sheet;

    public SpreadsheetAutoFillEngine(SpreadsheetSheet sheet)
    {
        _sheet = sheet;
    }

    /// <summary>Fills the target range based on the pattern in the source range.</summary>
    public void Fill(string sourceRangeRef, string targetRangeRef)
    {
        var source = SpreadsheetRange.Parse(sourceRangeRef);
        var target = SpreadsheetRange.Parse(targetRangeRef);

        var isVertical = source.RowCount > 1 || target.RowCount > source.RowCount;
        var isHorizontal = source.ColumnCount > 1 || target.ColumnCount > source.ColumnCount;

        if (isVertical && !isHorizontal)
        {
            FillVertical(source, target);
        }
        else if (isHorizontal && !isVertical)
        {
            FillHorizontal(source, target);
        }
        else if (source.RowCount == 1 && source.ColumnCount == 1)
        {
            // Single cell repeat
            FillSingle(source, target);
        }
        else
        {
            // Multi-dimensional: treat as vertical for simplicity
            FillVertical(source, target);
        }
    }

    private void FillVertical(SpreadsheetRange source, SpreadsheetRange target)
    {
        var values = GetColumnValues(source);
        var pattern = DetectPattern(values);

        var col = source.StartCol;
        for (var row = target.StartRow; row <= target.EndRow; row++)
        {
            var index = row - target.StartRow;
            var value = pattern.GetValue(index);
            var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
            _sheet.Cells[cellRef] = new SpreadsheetCell { Value = value };
        }
    }

    private void FillHorizontal(SpreadsheetRange source, SpreadsheetRange target)
    {
        var values = GetRowValues(source);
        var pattern = DetectPattern(values);

        var row = source.StartRow;
        for (var col = target.StartCol; col <= target.EndCol; col++)
        {
            var index = col - target.StartCol;
            var value = pattern.GetValue(index);
            var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
            _sheet.Cells[cellRef] = new SpreadsheetCell { Value = value };
        }
    }

    private void FillSingle(SpreadsheetRange source, SpreadsheetRange target)
    {
        var sourceRef = $"{SpreadsheetRange.ColumnIndexToLetters(source.StartCol)}{source.StartRow + 1}";
        var value = _sheet.Cells.GetValueOrDefault(sourceRef)?.Value;

        foreach (var cellRef in target.CellRefs)
        {
            _sheet.Cells[cellRef] = new SpreadsheetCell { Value = value };
        }
    }

    private List<object?> GetColumnValues(SpreadsheetRange range)
    {
        var result = new List<object?>();
        for (var row = range.StartRow; row <= range.EndRow; row++)
        {
            var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(range.StartCol)}{row + 1}";
            result.Add(_sheet.Cells.GetValueOrDefault(cellRef)?.Value);
        }
        return result;
    }

    private List<object?> GetRowValues(SpreadsheetRange range)
    {
        var result = new List<object?>();
        for (var col = range.StartCol; col <= range.EndCol; col++)
        {
            var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{range.StartRow + 1}";
            result.Add(_sheet.Cells.GetValueOrDefault(cellRef)?.Value);
        }
        return result;
    }

    private static IPattern DetectPattern(List<object?> values)
    {
        if (values.Count == 0)
            return new RepeatPattern(null);

        if (values.Count >= 2 && values[0] is double d1 && values[1] is double d2)
        {
            var step = d2 - d1;
            // If values look like dates (whole numbers, typical date range)
            if (Math.Abs(step) == 1.0 && values.All(v => v is double dd && dd > 30000 && dd == Math.Floor(dd)))
                return new DatePattern(d1, step);
            return new NumberPattern(d1, step);
        }

        // Text with trailing number
        if (values.Count >= 2 && values[0] is string s1 && values[1] is string s2)
        {
            var match1 = TrailingNumberRegex.Match(s1);
            var match2 = TrailingNumberRegex.Match(s2);
            if (match1.Success && match2.Success &&
                match1.Groups[1].Value == match2.Groups[1].Value &&
                int.TryParse(match1.Groups[2].Value, out var n1) &&
                int.TryParse(match2.Groups[2].Value, out var n2))
            {
                return new TextNumberPattern(match1.Groups[1].Value, n1, n2 - n1);
            }
        }

        if (values.Count == 1)
            return new RepeatPattern(values[0]);

        return new RepeatPattern(values[0]);
    }

    private static readonly Regex TrailingNumberRegex = new Regex(@"^(.*?)(\d+)$", RegexOptions.Compiled);

    private interface IPattern
    {
        object? GetValue(int index);
    }

    private sealed class NumberPattern : IPattern
    {
        private readonly double _start;
        private readonly double _step;
        public NumberPattern(double start, double step) { _start = start; _step = step; }
        public object? GetValue(int index) => _start + _step * index;
    }

    private sealed class DatePattern : IPattern
    {
        private readonly double _start;
        private readonly double _step;
        public DatePattern(double start, double step) { _start = start; _step = step; }
        public object? GetValue(int index) => _start + _step * index;
    }

    private sealed class TextNumberPattern : IPattern
    {
        private readonly string _prefix;
        private readonly int _start;
        private readonly int _step;
        public TextNumberPattern(string prefix, int start, int step) { _prefix = prefix; _start = start; _step = step; }
        public object? GetValue(int index) => $"{_prefix}{_start + _step * index}";
    }

    private sealed class RepeatPattern : IPattern
    {
        private readonly object? _value;
        public RepeatPattern(object? value) { _value = value; }
        public object? GetValue(int index) => _value;
    }
}
