using System.Globalization;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents a rectangular range of cells within a spreadsheet sheet.
/// Supports parsing from standard A1 notation (e.g. A1, B2:D10, $A$1).
/// </summary>
public sealed partial class SpreadsheetRange
{
    private static readonly Regex RangeRegex = GenerateRangeRegex();

    /// <summary>The zero-based starting row index.</summary>
    public int StartRow { get; }

    /// <summary>The zero-based starting column index.</summary>
    public int StartCol { get; }

    /// <summary>The zero-based ending row index (inclusive).</summary>
    public int EndRow { get; }

    /// <summary>The zero-based ending column index (inclusive).</summary>
    public int EndCol { get; }

    /// <summary>The number of rows in the range.</summary>
    public int RowCount => EndRow - StartRow + 1;

    /// <summary>The number of columns in the range.</summary>
    public int ColumnCount => EndCol - StartCol + 1;

    /// <summary>The total number of cells in the range.</summary>
    public int CellCount => RowCount * ColumnCount;

    /// <summary>Returns true if the given cell coordinates are within this range.</summary>
    public bool Contains(int row, int col) => row >= StartRow && row <= EndRow && col >= StartCol && col <= EndCol;

    public SpreadsheetRange(int startRow, int startCol, int endRow, int endCol)
    {
        StartRow = startRow;
        StartCol = startCol;
        EndRow = endRow;
        EndCol = endCol;
    }

    /// <summary>
    /// Parses a range string in A1 notation.
    /// Supports single cells (A1), ranges (A1:B10), and absolute references ($A$1).
    /// </summary>
    public static SpreadsheetRange Parse(string range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(range);

        var match = RangeRegex.Match(range.Replace("$", string.Empty));
        if (!match.Success)
            throw new FormatException($"Invalid range format: '{range}'. Expected A1 or A1:B10 notation.");

        var startCol = ColumnLettersToIndex(match.Groups["startCol"].Value);
        var startRow = int.Parse(match.Groups["startRow"].Value, CultureInfo.InvariantCulture) - 1;

        if (match.Groups["endCol"].Success)
        {
            var endCol = ColumnLettersToIndex(match.Groups["endCol"].Value);
            var endRow = int.Parse(match.Groups["endRow"].Value, CultureInfo.InvariantCulture) - 1;
            return new SpreadsheetRange(startRow, startCol, endRow, endCol);
        }

        return new SpreadsheetRange(startRow, startCol, startRow, startCol);
    }

    /// <summary>
    /// Converts a zero-based column index to Excel-style letters (0=A, 25=Z, 26=AA).
    /// </summary>
    public static string ColumnIndexToLetters(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var result = new List<char>();
        var value = index + 1;

        while (value > 0)
        {
            value--;
            result.Add((char)('A' + (value % 26)));
            value /= 26;
        }

        result.Reverse();
        return new string([.. result]);
    }

    /// <summary>
    /// Converts Excel-style column letters to a zero-based column index.
    /// </summary>
    public static int ColumnLettersToIndex(string letters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(letters);

        var result = 0;
        foreach (var c in letters.ToUpperInvariant())
        {
            if (c is < 'A' or > 'Z')
                throw new FormatException($"Invalid column letters: '{letters}'.");

            result = result * 26 + (c - 'A' + 1);
        }

        return result - 1;
    }

    /// <summary>
    /// Enumerates all cell references within this range in row-major order.
    /// Each reference is in A1 notation (e.g. A1, A2, B1, B2).
    /// </summary>
    public IEnumerable<string> CellRefs
    {
        get
        {
            for (var row = StartRow; row <= EndRow; row++)
            {
                for (var col = StartCol; col <= EndCol; col++)
                {
                    yield return $"{ColumnIndexToLetters(col)}{row + 1}";
                }
            }
        }
    }

    /// <summary>
    /// Returns the A1 notation string for this range.
    /// </summary>
    public override string ToString()
    {
        var start = $"{ColumnIndexToLetters(StartCol)}{StartRow + 1}";
        var end = $"{ColumnIndexToLetters(EndCol)}{EndRow + 1}";
        return StartRow == EndRow && StartCol == EndCol ? start : $"{start}:{end}";
    }

    [GeneratedRegex(@"^(?<startCol>[A-Za-z]+)(?<startRow>\d+)(:(?<endCol>[A-Za-z]+)(?<endRow>\d+))?$")]
    private static partial Regex GenerateRangeRegex();
}
