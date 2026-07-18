using System.Globalization;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Evaluation context for document-assembly expressions: token values, an optional repeating-row scope, and an injected clock.</summary>
public sealed class DocumentAssemblyContext
{
    /// <summary>Resolved token values keyed by token key.</summary>
    public IReadOnlyDictionary<string, DocumentTokenValue> TokenValues { get; init; } =
        new Dictionary<string, DocumentTokenValue>();

    /// <summary>Current repeating-section row scope; row columns shadow token keys.</summary>
    public IReadOnlyDictionary<string, string?>? RowScope { get; init; }

    /// <summary>Clock injected for deterministic TODAY()/date arithmetic.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Result of a document-assembly expression: a string, number, boolean, or date.</summary>
public readonly struct DocumentAssemblyValue
{
    private readonly decimal? _number;
    private readonly bool? _boolean;
    private readonly DateOnly? _date;
    private readonly string? _text;

    private DocumentAssemblyValue(decimal? number, bool? boolean, DateOnly? date, string? text)
    {
        _number = number;
        _boolean = boolean;
        _date = date;
        _text = text;
    }

    /// <summary>Creates a numeric value.</summary>
    public static DocumentAssemblyValue FromNumber(decimal value) => new(value, null, null, null);

    /// <summary>Creates a boolean value.</summary>
    public static DocumentAssemblyValue FromBoolean(bool value) => new(null, value, null, null);

    /// <summary>Creates a date value.</summary>
    public static DocumentAssemblyValue FromDate(DateOnly value) => new(null, null, value, null);

    /// <summary>Creates a string value.</summary>
    public static DocumentAssemblyValue FromText(string? value) => new(null, null, null, value ?? string.Empty);

    /// <summary>Numeric payload when the value is a number.</summary>
    public decimal? Number => _number;

    /// <summary>Date payload when the value is a date.</summary>
    public DateOnly? Date => _date;

    /// <summary>Converts the value to its culture-invariant string form.</summary>
    public string ToInvariantString()
        => _number is { } number ? number.ToString("0.############", CultureInfo.InvariantCulture)
            : _boolean is { } boolean ? (boolean ? "true" : "false")
            : _date is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : _text ?? string.Empty;

    /// <summary>Converts the value to a boolean (non-zero numbers and non-empty strings are true).</summary>
    public bool ToBoolean()
        => _boolean ?? (_number is { } number
            ? number != 0
            : _date is not null || !string.IsNullOrEmpty(_text));

    /// <summary>Attempts to view the value as a number (parsing strings invariantly).</summary>
    public decimal? AsNumber()
        => _number ?? (_boolean is { } boolean
            ? (boolean ? 1 : 0)
            : decimal.TryParse(_text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);

    /// <summary>Attempts to view the value as a date (ISO yyyy-MM-dd).</summary>
    public DateOnly? AsDate()
        => _date ?? (DateOnly.TryParseExact(_text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null);
}

/// <summary>
/// Pure, deterministic expression evaluator for document assembly: conditions over token values
/// (comparisons, boolean logic), arithmetic, SUM/COUNT over collection rows, CURRENCY/FORMAT
/// formatting, and date arithmetic (DATEADD, TODAY from the injected clock). Culture-invariant;
/// throws <see cref="FormatException"/> on malformed expressions.
/// </summary>
public static class DocumentAssemblyExpression
{
    /// <summary>Evaluates an expression against the supplied context.</summary>
    public static DocumentAssemblyValue Evaluate(string expression, DocumentAssemblyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new FormatException("Assembly expression is empty.");
        }

        var parser = new Parser(expression, context);
        var value = parser.ParseExpression();
        parser.ExpectEnd();
        return value;
    }

    /// <summary>Evaluates a condition expression to a boolean.</summary>
    public static bool EvaluateCondition(string expression, DocumentAssemblyContext context)
        => Evaluate(expression, context).ToBoolean();

    private sealed class Parser
    {
        private readonly string _input;
        private readonly DocumentAssemblyContext _context;
        private int _position;

        public Parser(string input, DocumentAssemblyContext context)
        {
            _input = input;
            _context = context;
        }

        public DocumentAssemblyValue ParseExpression() => ParseOr();

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_position < _input.Length)
            {
                throw new FormatException($"Unexpected input at position {_position}: '{_input[_position..]}'.");
            }
        }

        private DocumentAssemblyValue ParseOr()
        {
            var left = ParseAnd();
            while (TryConsume("||"))
            {
                var right = ParseAnd();
                left = DocumentAssemblyValue.FromBoolean(left.ToBoolean() || right.ToBoolean());
            }

            return left;
        }

        private DocumentAssemblyValue ParseAnd()
        {
            var left = ParseComparison();
            while (TryConsume("&&"))
            {
                var right = ParseComparison();
                left = DocumentAssemblyValue.FromBoolean(left.ToBoolean() && right.ToBoolean());
            }

            return left;
        }

        private DocumentAssemblyValue ParseComparison()
        {
            var left = ParseAdditive();
            foreach (var op in (string[])["==", "!=", "<=", ">=", "<", ">"])
            {
                if (TryConsume(op))
                {
                    var right = ParseAdditive();
                    return DocumentAssemblyValue.FromBoolean(Compare(left, right, op));
                }
            }

            return left;
        }

        private DocumentAssemblyValue ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                if (TryConsume("+"))
                {
                    var right = ParseMultiplicative();
                    left = Add(left, right);
                }
                else if (TryConsumeMinus())
                {
                    var right = ParseMultiplicative();
                    left = DocumentAssemblyValue.FromNumber(RequireNumber(left) - RequireNumber(right));
                }
                else
                {
                    return left;
                }
            }
        }

        private DocumentAssemblyValue ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                if (TryConsume("*"))
                {
                    var right = ParseUnary();
                    left = DocumentAssemblyValue.FromNumber(RequireNumber(left) * RequireNumber(right));
                }
                else if (TryConsume("/"))
                {
                    var right = ParseUnary();
                    var divisor = RequireNumber(right);
                    if (divisor == 0)
                    {
                        throw new FormatException("Division by zero in assembly expression.");
                    }

                    left = DocumentAssemblyValue.FromNumber(RequireNumber(left) / divisor);
                }
                else
                {
                    return left;
                }
            }
        }

        private DocumentAssemblyValue ParseUnary()
        {
            SkipWhitespace();
            if (TryConsume("!"))
            {
                return DocumentAssemblyValue.FromBoolean(!ParseUnary().ToBoolean());
            }

            if (TryConsumeMinus())
            {
                return DocumentAssemblyValue.FromNumber(-RequireNumber(ParseUnary()));
            }

            return ParsePrimary();
        }

        private DocumentAssemblyValue ParsePrimary()
        {
            SkipWhitespace();
            if (_position >= _input.Length)
            {
                throw new FormatException("Unexpected end of assembly expression.");
            }

            var current = _input[_position];
            if (current == '(')
            {
                _position++;
                var inner = ParseExpression();
                Expect(')');
                return inner;
            }

            if (current == '\'')
            {
                return DocumentAssemblyValue.FromText(ParseStringLiteral());
            }

            if (char.IsDigit(current))
            {
                return DocumentAssemblyValue.FromNumber(ParseNumberLiteral());
            }

            if (char.IsLetter(current) || current == '_')
            {
                var identifier = ParseIdentifier();
                SkipWhitespace();
                if (_position < _input.Length && _input[_position] == '(')
                {
                    return CallFunction(identifier);
                }

                return identifier switch
                {
                    "true" => DocumentAssemblyValue.FromBoolean(true),
                    "false" => DocumentAssemblyValue.FromBoolean(false),
                    _ => ResolveIdentifier(identifier),
                };
            }

            throw new FormatException($"Unexpected character '{current}' at position {_position}.");
        }

        // ── Functions ───────────────────────────────────────────────────────────────────────────

        private DocumentAssemblyValue CallFunction(string name)
        {
            Expect('(');
            var arguments = new List<DocumentAssemblyValue>();
            var rawArguments = new List<string>();
            SkipWhitespace();
            if (_position < _input.Length && _input[_position] != ')')
            {
                while (true)
                {
                    var argumentStart = _position;
                    arguments.Add(ParseExpression());
                    rawArguments.Add(_input[argumentStart.._position].Trim());
                    SkipWhitespace();
                    if (!TryConsume(","))
                    {
                        break;
                    }
                }
            }

            Expect(')');
            return name.ToUpperInvariant() switch
            {
                "SUM" => Sum(rawArguments, arguments),
                "COUNT" => Count(rawArguments),
                "CURRENCY" => Currency(arguments),
                "FORMAT" => Format(arguments),
                "DATEADD" => DateAdd(arguments),
                "TODAY" => DocumentAssemblyValue.FromDate(DateOnly.FromDateTime(_context.Now.UtcDateTime)),
                "NOW" => DocumentAssemblyValue.FromText(_context.Now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                _ => throw new FormatException($"Unknown assembly function '{name}'."),
            };
        }

        private DocumentAssemblyValue Sum(List<string> rawArguments, List<DocumentAssemblyValue> arguments)
        {
            if (rawArguments.Count != 2)
            {
                throw new FormatException("SUM(rows, 'column') expects exactly two arguments.");
            }

            var rows = ResolveRows(rawArguments[0]);
            var column = arguments[1].ToInvariantString();
            decimal total = 0;
            foreach (var row in rows)
            {
                if (row.TryGetValue(column, out var cell)
                    && decimal.TryParse(cell, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    total += number;
                }
            }

            return DocumentAssemblyValue.FromNumber(total);
        }

        private DocumentAssemblyValue Count(List<string> rawArguments)
        {
            if (rawArguments.Count != 1)
            {
                throw new FormatException("COUNT(rows) expects exactly one argument.");
            }

            return DocumentAssemblyValue.FromNumber(ResolveRows(rawArguments[0]).Count);
        }

        private static DocumentAssemblyValue Currency(List<DocumentAssemblyValue> arguments)
        {
            if (arguments.Count is < 1 or > 3)
            {
                throw new FormatException("CURRENCY(value, 'culture'?, 'currencySymbol'?) expects one to three arguments.");
            }

            var number = arguments[0].AsNumber()
                ?? throw new FormatException("CURRENCY() requires a numeric value.");
            var culture = arguments.Count >= 2
                ? CultureInfo.GetCultureInfo(arguments[1].ToInvariantString())
                : CultureInfo.InvariantCulture;
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
            if (arguments.Count == 3)
            {
                format.CurrencySymbol = arguments[2].ToInvariantString() switch
                {
                    "CZK" => "Kč",
                    "EUR" => "€",
                    "USD" => "$",
                    var symbol => symbol,
                };
            }

            return DocumentAssemblyValue.FromText(number.ToString("C2", format));
        }

        private static DocumentAssemblyValue Format(List<DocumentAssemblyValue> arguments)
        {
            if (arguments.Count != 2)
            {
                throw new FormatException("FORMAT(value, 'format') expects exactly two arguments.");
            }

            var format = arguments[1].ToInvariantString();
            if (arguments[0].AsNumber() is { } number)
            {
                return DocumentAssemblyValue.FromText(number.ToString(format, CultureInfo.InvariantCulture));
            }

            if (arguments[0].AsDate() is { } date)
            {
                return DocumentAssemblyValue.FromText(date.ToString(format, CultureInfo.InvariantCulture));
            }

            return DocumentAssemblyValue.FromText(arguments[0].ToInvariantString());
        }

        private static DocumentAssemblyValue DateAdd(List<DocumentAssemblyValue> arguments)
        {
            if (arguments.Count != 2)
            {
                throw new FormatException("DATEADD(date, days) expects exactly two arguments.");
            }

            var date = arguments[0].AsDate()
                ?? throw new FormatException("DATEADD() requires an ISO date (yyyy-MM-dd).");
            var days = arguments[1].AsNumber()
                ?? throw new FormatException("DATEADD() requires a numeric day count.");
            return DocumentAssemblyValue.FromDate(date.AddDays((int)days));
        }

        private IReadOnlyList<IReadOnlyDictionary<string, string?>> ResolveRows(string tokenKey)
        {
            if (_context.TokenValues.TryGetValue(tokenKey.Trim(), out var value) && value.Rows is { } rows)
            {
                return rows;
            }

            return [];
        }

        // ── Identifier / literal parsing ────────────────────────────────────────────────────────

        private DocumentAssemblyValue ResolveIdentifier(string identifier)
        {
            if (_context.RowScope is { } row)
            {
                if (row.TryGetValue(identifier, out var direct))
                {
                    return FromRaw(direct);
                }

                var dotIndex = identifier.IndexOf('.');
                if (dotIndex > 0 && row.TryGetValue(identifier[(dotIndex + 1)..], out var suffix))
                {
                    return FromRaw(suffix);
                }
            }

            if (_context.TokenValues.TryGetValue(identifier, out var token) && token.HasValue)
            {
                return FromRaw(token.Value);
            }

            return DocumentAssemblyValue.FromText(string.Empty);
        }

        private static DocumentAssemblyValue FromRaw(string? raw)
            => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? DocumentAssemblyValue.FromNumber(number)
                : DocumentAssemblyValue.FromText(raw);

        private string ParseIdentifier()
        {
            var start = _position;
            while (_position < _input.Length
                   && (char.IsLetterOrDigit(_input[_position]) || _input[_position] is '_' or '.'))
            {
                _position++;
            }

            return _input[start.._position];
        }

        private decimal ParseNumberLiteral()
        {
            var start = _position;
            while (_position < _input.Length && (char.IsDigit(_input[_position]) || _input[_position] == '.'))
            {
                _position++;
            }

            var slice = _input[start.._position];
            return decimal.TryParse(slice, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"Invalid number literal '{slice}'.");
        }

        private string ParseStringLiteral()
        {
            Expect('\'');
            var start = _position;
            while (_position < _input.Length && _input[_position] != '\'')
            {
                _position++;
            }

            if (_position >= _input.Length)
            {
                throw new FormatException("Unterminated string literal in assembly expression.");
            }

            var value = _input[start.._position];
            _position++;
            return value;
        }

        // ── Operator helpers ────────────────────────────────────────────────────────────────────

        private static bool Compare(DocumentAssemblyValue left, DocumentAssemblyValue right, string op)
        {
            var leftNumber = left.AsNumber();
            var rightNumber = right.AsNumber();
            if (leftNumber is { } l && rightNumber is { } r)
            {
                return op switch
                {
                    "==" => l == r,
                    "!=" => l != r,
                    "<" => l < r,
                    ">" => l > r,
                    "<=" => l <= r,
                    ">=" => l >= r,
                    _ => throw new FormatException($"Unknown operator '{op}'."),
                };
            }

            var comparison = string.CompareOrdinal(left.ToInvariantString(), right.ToInvariantString());
            return op switch
            {
                "==" => comparison == 0,
                "!=" => comparison != 0,
                "<" => comparison < 0,
                ">" => comparison > 0,
                "<=" => comparison <= 0,
                ">=" => comparison >= 0,
                _ => throw new FormatException($"Unknown operator '{op}'."),
            };
        }

        private static DocumentAssemblyValue Add(DocumentAssemblyValue left, DocumentAssemblyValue right)
        {
            if (left.AsNumber() is { } l && right.AsNumber() is { } r)
            {
                return DocumentAssemblyValue.FromNumber(l + r);
            }

            return DocumentAssemblyValue.FromText(left.ToInvariantString() + right.ToInvariantString());
        }

        private decimal RequireNumber(DocumentAssemblyValue value)
            => value.AsNumber()
               ?? throw new FormatException("Assembly expression expected a numeric value.");

        private bool TryConsume(string token)
        {
            SkipWhitespace();
            if (_input.AsSpan(_position).StartsWith(token, StringComparison.Ordinal))
            {
                // Do not consume '<'/'>' when they are the start of '<='/'>='; single-char compare
                // tokens are only tried after the two-char forms, so a direct match is safe.
                _position += token.Length;
                return true;
            }

            return false;
        }

        // '-' must not swallow the '-' of a negative comparison operand handled elsewhere; it is a
        // dedicated helper so ParseAdditive/ParseUnary read clearly.
        private bool TryConsumeMinus()
        {
            SkipWhitespace();
            if (_position < _input.Length && _input[_position] == '-')
            {
                _position++;
                return true;
            }

            return false;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();
            if (_position >= _input.Length || _input[_position] != expected)
            {
                throw new FormatException($"Expected '{expected}' at position {_position}.");
            }

            _position++;
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                _position++;
            }
        }
    }
}
