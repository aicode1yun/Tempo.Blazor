using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Tokenizes an Excel-like formula string into a sequence of <see cref="FormulaToken"/>s.
/// </summary>
public static partial class FormulaLexer
{
    private static readonly Regex CellRefRegex = CellRefPattern();

    /// <summary>Tokenizes the given formula string.</summary>
    public static List<FormulaToken> Tokenize(string formula)
    {
        var tokens = new List<FormulaToken>();
        var span = formula.AsSpan();
        var pos = 0;

        while (pos < span.Length)
        {
            var ch = span[pos];

            if (char.IsWhiteSpace(ch))
            {
                pos++;
                continue;
            }

            // Two-char operators
            if (pos + 1 < span.Length)
            {
                var two = span.Slice(pos, 2).ToString();
                switch (two)
                {
                    case "<>": tokens.Add(new(TokenType.NotEqual, "<>")); pos += 2; continue;
                    case "<=": tokens.Add(new(TokenType.LessThanOrEqual, "<=")); pos += 2; continue;
                    case ">=": tokens.Add(new(TokenType.GreaterThanOrEqual, ">=")); pos += 2; continue;
                }
            }

            // Single-char operators & delimiters
            switch (ch)
            {
                case '+': tokens.Add(new(TokenType.Plus, "+")); pos++; continue;
                case '-': tokens.Add(new(TokenType.Minus, "-")); pos++; continue;
                case '*': tokens.Add(new(TokenType.Multiply, "*")); pos++; continue;
                case '/': tokens.Add(new(TokenType.Divide, "/")); pos++; continue;
                case '^': tokens.Add(new(TokenType.Power, "^")); pos++; continue;
                case '%': tokens.Add(new(TokenType.Percent, "%")); pos++; continue;
                case '&': tokens.Add(new(TokenType.Ampersand, "&")); pos++; continue;
                case '=': tokens.Add(new(TokenType.Equal, "=")); pos++; continue;
                case '<': tokens.Add(new(TokenType.LessThan, "<")); pos++; continue;
                case '>': tokens.Add(new(TokenType.GreaterThan, ">")); pos++; continue;
                case ',': tokens.Add(new(TokenType.Comma, ",")); pos++; continue;
                case ';': tokens.Add(new(TokenType.Semicolon, ";")); pos++; continue;
                case ':': tokens.Add(new(TokenType.Colon, ":")); pos++; continue;
                case '(': tokens.Add(new(TokenType.LParen, "(")); pos++; continue;
                case ')': tokens.Add(new(TokenType.RParen, ")")); pos++; continue;
            }

            // String literal
            if (ch == '"')
            {
                var sb = new StringBuilder();
                pos++; // skip opening quote
                while (pos < span.Length)
                {
                    if (span[pos] == '"')
                    {
                        if (pos + 1 < span.Length && span[pos + 1] == '"')
                        {
                            sb.Append('"');
                            pos += 2;
                            continue;
                        }
                        pos++; // skip closing quote
                        break;
                    }
                    sb.Append(span[pos]);
                    pos++;
                }
                tokens.Add(new(TokenType.String, sb.ToString()));
                continue;
            }

            // Number
            if (char.IsDigit(ch) || (ch == '.' && pos + 1 < span.Length && char.IsDigit(span[pos + 1])))
            {
                var start = pos;
                while (pos < span.Length && (char.IsDigit(span[pos]) || span[pos] == '.'))
                    pos++;
                tokens.Add(new(TokenType.Number, span[start..pos].ToString()));
                continue;
            }

            // Identifier / CellRef / Boolean
            if (char.IsLetter(ch) || ch == '$')
            {
                var start = pos;
                while (pos < span.Length && (char.IsLetterOrDigit(span[pos]) || span[pos] == '$'))
                    pos++;
                var word = span[start..pos].ToString();

                if (word.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new(TokenType.Boolean, "TRUE"));
                    continue;
                }
                if (word.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new(TokenType.Boolean, "FALSE"));
                    continue;
                }

                // Cell reference or range reference lookahead
                if (CellRefRegex.IsMatch(word))
                {
                    // Look ahead for :CellRef to emit a RangeRef
                    if (pos < span.Length && span[pos] == ':')
                    {
                        var colonPos = pos;
                        pos++;
                        var endStart = pos;
                        while (pos < span.Length && (char.IsLetterOrDigit(span[pos]) || span[pos] == '$'))
                            pos++;
                        var endWord = span[endStart..pos].ToString();
                        if (CellRefRegex.IsMatch(endWord))
                        {
                            tokens.Add(new(TokenType.RangeRef, $"{word}:{endWord}"));
                            continue;
                        }
                        // Not a valid range – backtrack
                        pos = colonPos;
                    }
                    tokens.Add(new(TokenType.CellRef, word));
                    continue;
                }

                tokens.Add(new(TokenType.Identifier, word));
                continue;
            }

            // Unknown character – skip
            pos++;
        }

        tokens.Add(new(TokenType.End, string.Empty));
        return tokens;
    }

    [GeneratedRegex(@"^\$?[A-Za-z]+\$?\d+$", RegexOptions.Compiled)]
    private static partial Regex CellRefPattern();
}
