using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Extracts cell references from a formula string for dependency tracking.
/// </summary>
public static class FormulaDependencyExtractor
{
    /// <summary>Returns all unique cell references found in the formula.</summary>
    public static HashSet<string> ExtractCellRefs(string formula)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var tokens = FormulaLexer.Tokenize(formula);
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.CellRef)
                    refs.Add(token.Value.ToUpperInvariant());
                else if (token.Type == TokenType.RangeRef)
                {
                    var parts = token.Value.Split(':');
                    try
                    {
                        var range = SpreadsheetRange.Parse(parts[0] + ":" + parts[1]);
                        foreach (var cellRef in range.CellRefs)
                            refs.Add(cellRef.ToUpperInvariant());
                    }
                    catch { /* ignore invalid range */ }
                }
            }
        }
        catch
        {
            // If lexing fails, return empty set
        }
        return refs;
    }
}
