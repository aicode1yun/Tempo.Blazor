using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Adjusts cell references inside formula strings when cells are copied or auto-filled.
/// Relative references (e.g. A1) are moved by the given offset.
/// Absolute references (e.g. $A$1) are preserved.
/// Mixed references ($A1, A$1) adjust only the relative part.
/// </summary>
public static partial class FormulaReferenceAdjuster
{
    // Captures: (?<colAbs>$?)  (?<col>[A-Za-z]+)  (?<rowAbs>$?)  (?<row>\d+)
    private static readonly Regex CellRefComponentRegex = CellRefComponentPattern();

    /// <summary>
    /// Adjusts all relative cell references in <paramref name="formula"/> by
    /// <paramref name="dRow"/> rows and <paramref name="dCol"/> columns.
    /// Returns the adjusted formula string, or the original if parsing fails.
    /// </summary>
    public static string AdjustFormula(string formula, int dRow, int dCol)
    {
        if (dRow == 0 && dCol == 0) return formula;

        try
        {
            var tokens = FormulaLexer.Tokenize(formula);
            var sb = new StringBuilder();

            foreach (var token in tokens)
            {
                if (token.Type == TokenType.End) break;

                sb.Append(token.Type switch
                {
                    TokenType.CellRef  => AdjustCellRef(token.Value, dRow, dCol),
                    TokenType.RangeRef => AdjustRangeRef(token.Value, dRow, dCol),
                    TokenType.String   => $"\"{token.Value}\"",
                    _                  => token.Value
                });
            }

            return sb.ToString();
        }
        catch
        {
            return formula;
        }
    }

    /// <summary>
    /// Cycles the absolute/relative mode of the last cell reference in
    /// <paramref name="formula"/> through: A1 → $A$1 → A$1 → $A1 → A1.
    /// Returns the original formula if no cell reference is found.
    /// </summary>
    public static string CycleLastAbsoluteRef(string formula)
    {
        try
        {
            var tokens = FormulaLexer.Tokenize(formula);

            int lastIdx = -1;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type is TokenType.CellRef or TokenType.RangeRef)
                    lastIdx = i;
            }

            if (lastIdx < 0) return formula;

            var sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Type == TokenType.End) break;

                if (i == lastIdx)
                {
                    sb.Append(t.Type == TokenType.CellRef
                        ? CycleSingleRef(t.Value)
                        : AdjustRangeRefWith(t.Value, static s => CycleSingleRef(s)));
                }
                else
                {
                    sb.Append(t.Value);
                }
            }

            return sb.ToString();
        }
        catch
        {
            return formula;
        }
    }

    /// <summary>
    /// Adjusts a single cell reference string (e.g. "$A1", "B$3") by dRow/dCol.
    /// Returns "#REF!" if the adjusted position is out of bounds.
    /// </summary>
    public static string AdjustCellRef(string cellRef, int dRow, int dCol)
    {
        var m = CellRefComponentRegex.Match(cellRef);
        if (!m.Success) return cellRef;

        var colAbs    = m.Groups["colAbs"].Value == "$";
        var colLetters = m.Groups["col"].Value.ToUpperInvariant();
        var rowAbs    = m.Groups["rowAbs"].Value == "$";
        var rowNum    = int.Parse(m.Groups["row"].Value);

        string newCol;
        if (colAbs)
        {
            newCol = colLetters;
        }
        else
        {
            var colIdx = SpreadsheetRange.ColumnLettersToIndex(colLetters) + dCol;
            if (colIdx < 0) return "#REF!";
            newCol = SpreadsheetRange.ColumnIndexToLetters(colIdx);
        }

        int newRow;
        if (rowAbs)
        {
            newRow = rowNum;
        }
        else
        {
            newRow = rowNum + dRow;
            if (newRow < 1) return "#REF!";
        }

        return $"{(colAbs ? "$" : "")}{newCol}{(rowAbs ? "$" : "")}{newRow}";
    }

    /// <summary>
    /// Returns the unique raw cell/range references from <paramref name="formula"/> in order of
    /// first appearance. Used to assign highlight colours in formula-point mode.
    /// </summary>
    public static IReadOnlyList<string> ParseFormulaReferences(string formula)
    {
        try
        {
            var tokens = FormulaLexer.Tokenize(formula);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.End) break;
                if (token.Type is TokenType.CellRef or TokenType.RangeRef)
                {
                    if (seen.Add(token.Value.ToUpperInvariant()))
                        result.Add(token.Value);
                }
            }
            return result;
        }
        catch { return []; }
    }

    /// <summary>
    /// Inserts <paramref name="cellRef"/> into <paramref name="formula"/>: replaces the last
    /// CellRef/RangeRef token when present (user picked a new cell), otherwise appends.
    /// </summary>
    public static string InsertOrReplaceLastRef(string formula, string cellRef)
    {
        try
        {
            var tokens = FormulaLexer.Tokenize(formula);
            int lastIdx = -1;
            for (int i = 0; i < tokens.Count; i++)
                if (tokens[i].Type != TokenType.End)
                    lastIdx = i;

            if (lastIdx >= 0 && tokens[lastIdx].Type is TokenType.CellRef or TokenType.RangeRef)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < lastIdx; i++)
                {
                    var t = tokens[i];
                    sb.Append(t.Type == TokenType.String ? $"\"{t.Value}\"" : t.Value);
                }
                sb.Append(cellRef);
                return sb.ToString();
            }
            return formula + cellRef;
        }
        catch { return formula + cellRef; }
    }

    // ── Cycling helpers ──────────────────────────────────────────────────────

    /// <summary>Cycles a single cell ref through all four absolute/relative states.</summary>
    private static string CycleSingleRef(string cellRef)
    {
        var m = CellRefComponentRegex.Match(cellRef);
        if (!m.Success) return cellRef;

        var colAbs    = m.Groups["colAbs"].Value == "$";
        var colLetters = m.Groups["col"].Value.ToUpperInvariant();
        var rowAbs    = m.Groups["rowAbs"].Value == "$";
        var rowNum    = m.Groups["row"].Value;

        // A1 → $A$1 → A$1 → $A1 → A1
        var (nc, nr) = (colAbs, rowAbs) switch
        {
            (false, false) => (true,  true),
            (true,  true)  => (false, true),
            (false, true)  => (true,  false),
            (true,  false) => (false, false)
        };

        return $"{(nc ? "$" : "")}{colLetters}{(nr ? "$" : "")}{rowNum}";
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private static string AdjustRangeRef(string rangeRef, int dRow, int dCol)
        => AdjustRangeRefWith(rangeRef, s => AdjustCellRef(s, dRow, dCol));

    private static string AdjustRangeRefWith(string rangeRef, Func<string, string> transform)
    {
        var colon = rangeRef.IndexOf(':');
        if (colon < 0) return rangeRef;
        return $"{transform(rangeRef[..colon])}:{transform(rangeRef[(colon + 1)..])}";
    }

    [GeneratedRegex(@"^(?<colAbs>\$?)(?<col>[A-Za-z]+)(?<rowAbs>\$?)(?<row>\d+)$", RegexOptions.Compiled)]
    private static partial Regex CellRefComponentPattern();
}
