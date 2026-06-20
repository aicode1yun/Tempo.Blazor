using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Extracts cell references and named ranges from a formula string for dependency tracking.
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

    /// <summary>Returns all unique named range identifiers found in the formula.</summary>
    public static HashSet<string> ExtractNamedRanges(string formula)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var tokens = FormulaLexer.Tokenize(formula);
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Type == TokenType.Identifier)
                {
                    // It's a named range unless it's a function call
                    if (i + 1 >= tokens.Count || tokens[i + 1].Type != TokenType.LParen)
                        names.Add(token.Value);
                }
            }
        }
        catch
        {
            // If lexing fails, return empty set
        }
        return names;
    }
}
