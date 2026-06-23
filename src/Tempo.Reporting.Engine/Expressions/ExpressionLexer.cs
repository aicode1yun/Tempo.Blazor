using System.Globalization;

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Lexer for report expressions.</summary>
public static class ExpressionLexer
{
    /// <summary>Tokenizes an expression.</summary>
    public static IReadOnlyList<ExpressionToken> Tokenize(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new Scanner(expression).Tokenize();
    }

    private sealed class Scanner
    {
        private readonly string _text;
        private readonly List<ExpressionToken> _tokens = [];
        private int _index;
        private int _line = 1;
        private int _column = 1;

        public Scanner(string text)
        {
            _text = text;
        }

        public IReadOnlyList<ExpressionToken> Tokenize()
        {
            if (Current == '=')
            {
                Advance();
            }

            while (!IsAtEnd)
            {
                if (char.IsWhiteSpace(Current))
                {
                    AdvanceWhitespace();
                    continue;
                }

                if (char.IsDigit(Current))
                {
                    ScanNumber();
                    continue;
                }

                if (Current == '"')
                {
                    ScanString();
                    continue;
                }

                if (IsIdentifierStart(Current))
                {
                    ScanIdentifier();
                    continue;
                }

                ScanOperator();
            }

            _tokens.Add(new ExpressionToken(ExpressionTokenKind.EndOfInput, string.Empty, null, _line, _column));
            return _tokens;
        }

        private bool IsAtEnd => _index >= _text.Length;

        private char Current => IsAtEnd ? '\0' : _text[_index];

        private char Peek(int offset) => _index + offset >= _text.Length ? '\0' : _text[_index + offset];

        private void Advance()
        {
            if (IsAtEnd)
            {
                return;
            }

            var c = _text[_index++];
            if (c == '\r')
            {
                if (Current == '\n')
                {
                    _index++;
                }

                _line++;
                _column = 1;
            }
            else if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }

        private void AdvanceWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                Advance();
            }
        }

        private void ScanNumber()
        {
            var line = _line;
            var column = _column;
            var start = _index;

            while (char.IsDigit(Current))
            {
                Advance();
            }

            if (Current == '.' && char.IsDigit(Peek(1)))
            {
                Advance();
                while (char.IsDigit(Current))
                {
                    Advance();
                }
            }

            var text = _text[start.._index];
            var value = decimal.Parse(text, CultureInfo.InvariantCulture);
            _tokens.Add(new ExpressionToken(ExpressionTokenKind.Number, text, value, line, column));
        }

        private void ScanString()
        {
            var line = _line;
            var column = _column;
            Advance();
            var builder = new System.Text.StringBuilder();

            while (!IsAtEnd && Current != '"')
            {
                if (Current == '\\')
                {
                    Advance();
                    builder.Append(Current switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => Current,
                    });
                    Advance();
                    continue;
                }

                builder.Append(Current);
                Advance();
            }

            if (IsAtEnd)
            {
                throw new ExpressionParseException(ExpressionDiagnostics.Create(
                    "ExpressionLexer.UnterminatedString",
                    line,
                    column));
            }

            Advance();
            _tokens.Add(new ExpressionToken(ExpressionTokenKind.String, builder.ToString(), builder.ToString(), line, column));
        }

        private void ScanIdentifier()
        {
            var line = _line;
            var column = _column;
            var start = _index;
            Advance();
            while (IsIdentifierPart(Current))
            {
                Advance();
            }

            var text = _text[start.._index];
            _tokens.Add(new ExpressionToken(ExpressionTokenKind.Identifier, text, null, line, column));
        }

        private void ScanOperator()
        {
            var line = _line;
            var column = _column;
            var c = Current;
            Advance();

            var token = c switch
            {
                '.' => new ExpressionToken(ExpressionTokenKind.Dot, ".", null, line, column),
                ',' => new ExpressionToken(ExpressionTokenKind.Comma, ",", null, line, column),
                '(' => new ExpressionToken(ExpressionTokenKind.OpenParen, "(", null, line, column),
                ')' => new ExpressionToken(ExpressionTokenKind.CloseParen, ")", null, line, column),
                '+' => new ExpressionToken(ExpressionTokenKind.Plus, "+", null, line, column),
                '-' => new ExpressionToken(ExpressionTokenKind.Minus, "-", null, line, column),
                '*' => new ExpressionToken(ExpressionTokenKind.Star, "*", null, line, column),
                '/' => new ExpressionToken(ExpressionTokenKind.Slash, "/", null, line, column),
                '%' => new ExpressionToken(ExpressionTokenKind.Percent, "%", null, line, column),
                '=' => new ExpressionToken(ExpressionTokenKind.Equal, "=", null, line, column),
                '<' when Current == '=' => ConsumeTwo(ExpressionTokenKind.LessOrEqual, "<=", line, column),
                '<' when Current == '>' => ConsumeTwo(ExpressionTokenKind.NotEqual, "<>", line, column),
                '!' when Current == '=' => ConsumeTwo(ExpressionTokenKind.NotEqual, "!=", line, column),
                '<' => new ExpressionToken(ExpressionTokenKind.Less, "<", null, line, column),
                '>' when Current == '=' => ConsumeTwo(ExpressionTokenKind.GreaterOrEqual, ">=", line, column),
                '>' => new ExpressionToken(ExpressionTokenKind.Greater, ">", null, line, column),
                _ => throw new ExpressionParseException(ExpressionDiagnostics.Create(
                    "ExpressionLexer.UnexpectedCharacter",
                    line,
                    column,
                    c)),
            };

            _tokens.Add(token);
        }

        private ExpressionToken ConsumeTwo(ExpressionTokenKind kind, string text, int line, int column)
        {
            Advance();
            return new ExpressionToken(kind, text, null, line, column);
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
