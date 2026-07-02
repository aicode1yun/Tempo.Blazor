using System.Globalization;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>
/// A parsed safe binding expression. Public parsing APIs never throw on malformed input:
/// they return a literal-mode instance with <see cref="IsMalformed"/> set to <c>true</c>.
/// The only permitted function call is <c>token(key)</c>; any other call becomes a parse
/// error and is handled by the same literal fallback.
/// </summary>
public sealed class StencilExpression
{
    private const int MaxSourceLength = 32_768;
    private const int MaxParseDepth = 128;
    private const int MaxAstDepth = 128;
    private const int MaxNodeCount = 4_096;

    private StencilExpression(string raw, StencilExpressionNode root, bool isMalformed, string? error)
    {
        Raw = raw;
        Root = root;
        IsMalformed = isMalformed;
        Error = error;
    }

    public bool IsMalformed { get; }

    public string Raw { get; }

    public string? Error { get; }

    public StencilExpressionNode Root { get; }

    /// <summary>Parses <paramref name="text"/>; on failure returns a literal fallback and never throws.</summary>
    public static StencilExpression Parse(string? text)
    {
        TryParse(text, out var expression, out _);
        return expression;
    }

    /// <summary>Parses <paramref name="text"/> and reports diagnostics without throwing.</summary>
    public static bool TryParse(string? text, out StencilExpression expression, out string? error)
    {
        var raw = text ?? string.Empty;

        try
        {
            if (raw.Length > MaxSourceLength)
                throw new ParseException("Expression exceeds the maximum supported length.");

            var source = ExtractExpressionSource(raw, out var shouldParse);
            if (!shouldParse)
            {
                expression = LiteralExpression(raw, raw, isMalformed: false, error: null);
                error = null;
                return true;
            }

            if (source.StartsWith("$map{", StringComparison.Ordinal))
            {
                var map = ParseMap(source);
                expression = new StencilExpression(raw, map, isMalformed: false, error: null);
                error = null;
                return true;
            }

            var parser = new Parser(source);
            var root = parser.Parse();
            expression = new StencilExpression(raw, root, isMalformed: false, error: null);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            expression = LiteralExpression(raw, raw, isMalformed: true, error);
            return false;
        }
    }

    private static StencilExpression LiteralExpression(string raw, string value, bool isMalformed, string? error)
        => new(raw, StencilExpressionNode.Literal(value), isMalformed, error);

    private static string ExtractExpressionSource(string raw, out bool shouldParse)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            shouldParse = false;
            return raw;
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}') && trimmed.Length >= 2)
        {
            shouldParse = true;
            return trimmed[1..^1].Trim();
        }

        if (trimmed.StartsWith("$map{", StringComparison.Ordinal))
        {
            shouldParse = true;
            return trimmed;
        }

        if (trimmed.StartsWith('{') || trimmed.EndsWith('}'))
            throw new ParseException("Binding expression braces are not balanced.");

        shouldParse = trimmed.StartsWith("token(", StringComparison.Ordinal)
                      || trimmed.StartsWith('"')
                      || trimmed.StartsWith('\'')
                      || string.Equals(trimmed, "size.w", StringComparison.Ordinal)
                      || string.Equals(trimmed, "size.h", StringComparison.Ordinal)
                      || string.Equals(trimmed, "repeat.index", StringComparison.Ordinal);

        return trimmed;
    }

    private static StencilExpressionNode ParseMap(string source)
    {
        if (!source.EndsWith("}", StringComparison.Ordinal))
            throw new ParseException("$map expression must end with '}'.");

        var body = source["$map{".Length..^1].Trim();
        var colon = FindTopLevel(body, ':');
        var comma = FindTopLevel(body, ',');
        var usesCommaSourceSeparator = comma > 0 && (colon < 0 || comma < colon);
        var sourceSeparator = usesCommaSourceSeparator ? comma : colon;
        if (sourceSeparator <= 0)
            throw new ParseException("$map expression requires a source and entries.");

        var sourceExpression = new Parser(body[..sourceSeparator].Trim()).Parse();
        var entriesText = body[(sourceSeparator + 1)..];
        var entries = new Dictionary<string, StencilExpressionNode>(StringComparer.Ordinal);
        StencilExpressionNode? defaultNode = null;

        foreach (var item in SplitTopLevel(entriesText, ','))
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                continue;

            var equals = FindTopLevel(trimmed, '=');
            var entryColon = FindTopLevel(trimmed, ':');
            var separator = equals > 0 && (entryColon < 0 || equals < entryColon)
                ? equals
                : entryColon;
            if (separator <= 0)
                throw new ParseException("$map entries must use key=value or key: value.");

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            var node = ParseMapValue(value);

            if (key == "*" || key.Equals("default", StringComparison.OrdinalIgnoreCase))
                defaultNode = node;
            else
                entries[key] = node;
        }

        return StencilExpressionNode.Map(sourceExpression, entries, defaultNode ?? StencilExpressionNode.Null());
    }

    private static StencilExpressionNode ParseMapValue(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return StencilExpressionNode.Literal(string.Empty);

        var parseAsExpression = trimmed.StartsWith('{')
                                || trimmed.StartsWith("token(", StringComparison.Ordinal)
                                || trimmed.StartsWith('"')
                                || trimmed.StartsWith('\'')
                                || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                                || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(trimmed, "size.w", StringComparison.Ordinal)
                                || string.Equals(trimmed, "size.h", StringComparison.Ordinal)
                                || string.Equals(trimmed, "repeat.index", StringComparison.Ordinal);

        if (!parseAsExpression)
            return StencilExpressionNode.Literal(trimmed);

        var expression = Parse(trimmed);
        if (expression.IsMalformed)
            throw new ParseException(expression.Error ?? "Invalid $map entry value.");

        return expression.Root;
    }

    private static int FindTopLevel(string text, char target)
    {
        var depth = 0;
        var quote = '\0';

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == quote && (i == 0 || text[i - 1] != '\\'))
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c is '(' or '{' or '[')
                depth++;
            else if (c is ')' or '}' or ']')
                depth--;
            else if (depth == 0 && c == target)
                return i;
        }

        return -1;
    }

    private static IReadOnlyList<string> SplitTopLevel(string text, char separator)
    {
        var items = new List<string>();
        var depth = 0;
        var quote = '\0';
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == quote && (i == 0 || text[i - 1] != '\\'))
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c is '(' or '{' or '[')
                depth++;
            else if (c is ')' or '}' or ']')
                depth--;
            else if (depth == 0 && c == separator)
            {
                items.Add(text[start..i]);
                start = i + 1;
            }
        }

        items.Add(text[start..]);
        return items;
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _position;
        private int _parseDepth;

        public Parser(string source)
        {
            _tokens = Tokenizer.Tokenize(source);
        }

        public StencilExpressionNode Parse()
        {
            var expression = ParseConditional();
            Expect(TokenKind.End);
            ValidateAst(expression);
            return expression;
        }

        private StencilExpressionNode ParseConditional()
        {
            EnterRecursion();
            try
            {
                var condition = ParseCoalesce();
                if (!Match(TokenKind.Question))
                    return condition;

                var whenTrue = ParseConditional();
                Expect(TokenKind.Colon);
                var whenFalse = ParseConditional();
                return StencilExpressionNode.Conditional(condition, whenTrue, whenFalse);
            }
            finally
            {
                ExitRecursion();
            }
        }

        private StencilExpressionNode ParseCoalesce()
        {
            var left = ParseOr();
            while (Match(TokenKind.QuestionQuestion))
                left = StencilExpressionNode.Binary(StencilExpressionOperator.Coalesce, left, ParseOr());
            return left;
        }

        private StencilExpressionNode ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.OrOr))
                left = StencilExpressionNode.Binary(StencilExpressionOperator.Or, left, ParseAnd());
            return left;
        }

        private StencilExpressionNode ParseAnd()
        {
            var left = ParseEquality();
            while (Match(TokenKind.AndAnd))
                left = StencilExpressionNode.Binary(StencilExpressionOperator.And, left, ParseEquality());
            return left;
        }

        private StencilExpressionNode ParseEquality()
        {
            var left = ParseComparison();
            while (true)
            {
                if (Match(TokenKind.EqualEqual))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Equal, left, ParseComparison());
                else if (Match(TokenKind.BangEqual))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.NotEqual, left, ParseComparison());
                else
                    return left;
            }
        }

        private StencilExpressionNode ParseComparison()
        {
            var left = ParseAdditive();
            while (true)
            {
                if (Match(TokenKind.Greater))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Greater, left, ParseAdditive());
                else if (Match(TokenKind.GreaterOrEqual))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.GreaterOrEqual, left, ParseAdditive());
                else if (Match(TokenKind.Less))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Less, left, ParseAdditive());
                else if (Match(TokenKind.LessOrEqual))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.LessOrEqual, left, ParseAdditive());
                else
                    return left;
            }
        }

        private StencilExpressionNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                if (Match(TokenKind.Plus))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Add, left, ParseMultiplicative());
                else if (Match(TokenKind.Minus))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Subtract, left, ParseMultiplicative());
                else
                    return left;
            }
        }

        private StencilExpressionNode ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match(TokenKind.Star))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Multiply, left, ParseUnary());
                else if (Match(TokenKind.Slash))
                    left = StencilExpressionNode.Binary(StencilExpressionOperator.Divide, left, ParseUnary());
                else
                    return left;
            }
        }

        private StencilExpressionNode ParseUnary()
        {
            EnterRecursion();
            try
            {
                if (Match(TokenKind.Bang))
                    return StencilExpressionNode.Unary(StencilExpressionOperator.Not, ParseUnary());

                if (Match(TokenKind.Minus))
                    return StencilExpressionNode.Unary(StencilExpressionOperator.Negate, ParseUnary());

                return ParsePrimary();
            }
            finally
            {
                ExitRecursion();
            }
        }

        private StencilExpressionNode ParsePrimary()
        {
            if (Match(TokenKind.String, out var stringToken))
                return StencilExpressionNode.Literal(stringToken.Value);

            if (Match(TokenKind.Number, out var numberToken))
                return StencilExpressionNode.Literal(double.Parse(numberToken.Value, CultureInfo.InvariantCulture));

            if (Match(TokenKind.Identifier, out var identifier))
                return ParseIdentifier(identifier.Value);

            if (Match(TokenKind.LeftParen))
            {
                var expression = ParseConditional();
                Expect(TokenKind.RightParen);
                return expression;
            }

            throw new ParseException($"Unexpected token '{Peek().Value}'.");
        }

        private StencilExpressionNode ParseIdentifier(string identifier)
        {
            if (string.Equals(identifier, "true", StringComparison.OrdinalIgnoreCase))
                return StencilExpressionNode.Literal(true);

            if (string.Equals(identifier, "false", StringComparison.OrdinalIgnoreCase))
                return StencilExpressionNode.Literal(false);

            if (string.Equals(identifier, "null", StringComparison.OrdinalIgnoreCase))
                return StencilExpressionNode.Null();

            if (identifier == "size.w")
                return StencilExpressionNode.Simple(StencilExpressionNodeKind.SizeWidth);

            if (identifier == "size.h")
                return StencilExpressionNode.Simple(StencilExpressionNodeKind.SizeHeight);

            if (identifier == "repeat.index")
                return StencilExpressionNode.Simple(StencilExpressionNodeKind.RepeatIndex);

            if (Match(TokenKind.LeftParen))
            {
                if (identifier != "token")
                    throw new ParseException($"Function '{identifier}' is not allowed.");

                var key = ParseConditional();
                StencilExpressionNode? fallback = null;
                if (Match(TokenKind.Comma))
                    fallback = ParseConditional();

                Expect(TokenKind.RightParen);

                if (key.Kind != StencilExpressionNodeKind.Literal || key.Value is not string tokenKey)
                    throw new ParseException("token() key must be a string literal.");

                return StencilExpressionNode.Token(tokenKey, fallback);
            }

            return StencilExpressionNode.Property(identifier);
        }

        private bool Match(TokenKind kind)
        {
            if (Peek().Kind != kind)
                return false;

            _position++;
            return true;
        }

        private bool Match(TokenKind kind, out Token token)
        {
            token = Peek();
            if (token.Kind != kind)
                return false;

            _position++;
            return true;
        }

        private void Expect(TokenKind kind)
        {
            if (!Match(kind, out var token))
                throw new ParseException($"Expected {kind}, got '{token.Value}'.");
        }

        private Token Peek() => _tokens[Math.Min(_position, _tokens.Count - 1)];

        private void EnterRecursion()
        {
            _parseDepth++;
            if (_parseDepth > MaxParseDepth)
                throw new ParseException("Expression nesting exceeds the maximum supported depth.");
        }

        private void ExitRecursion() => _parseDepth--;

        private static void ValidateAst(StencilExpressionNode root)
        {
            var stack = new Stack<(StencilExpressionNode Node, int Depth)>();
            stack.Push((root, 1));
            var count = 0;

            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();
                if (depth > MaxAstDepth)
                    throw new ParseException("Expression AST exceeds the maximum supported depth.");

                count++;
                if (count > MaxNodeCount)
                    throw new ParseException("Expression AST exceeds the maximum supported node count.");

                Push(node.Left);
                Push(node.Right);
                Push(node.Operand);
                Push(node.Condition);
                Push(node.WhenTrue);
                Push(node.WhenFalse);
                Push(node.Source);
                Push(node.Default);

                foreach (var entry in node.MapEntries.Values)
                    Push(entry);

                void Push(StencilExpressionNode? child)
                {
                    if (child is not null)
                        stack.Push((child, depth + 1));
                }
            }
        }
    }

    private static class Tokenizer
    {
        public static IReadOnlyList<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            var index = 0;

            while (index < source.Length)
            {
                var c = source[index];
                if (char.IsWhiteSpace(c))
                {
                    index++;
                    continue;
                }

                if (c is '"' or '\'')
                {
                    tokens.Add(ReadString(source, ref index));
                    continue;
                }

                if (char.IsDigit(c))
                {
                    tokens.Add(ReadNumber(source, ref index));
                    continue;
                }

                if (IsIdentifierStart(c))
                {
                    tokens.Add(ReadIdentifier(source, ref index));
                    continue;
                }

                tokens.Add(ReadSymbol(source, ref index));
            }

            tokens.Add(new Token(TokenKind.End, string.Empty));
            return tokens;
        }

        private static Token ReadString(string source, ref int index)
        {
            var quote = source[index++];
            var value = new System.Text.StringBuilder();

            while (index < source.Length)
            {
                var c = source[index++];
                if (c == quote)
                    return new Token(TokenKind.String, value.ToString());

                if (c == '\\' && index < source.Length)
                {
                    var escaped = source[index++];
                    value.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        '\'' => '\'',
                        _ => escaped
                    });
                    continue;
                }

                value.Append(c);
            }

            throw new ParseException("Unterminated string literal.");
        }

        private static Token ReadNumber(string source, ref int index)
        {
            var start = index;
            while (index < source.Length && (char.IsDigit(source[index]) || source[index] == '.'))
                index++;

            return new Token(TokenKind.Number, source[start..index]);
        }

        private static Token ReadIdentifier(string source, ref int index)
        {
            var start = index;
            while (index < source.Length && IsIdentifierPart(source[index]))
                index++;

            return new Token(TokenKind.Identifier, source[start..index]);
        }

        private static Token ReadSymbol(string source, ref int index)
        {
            var remaining = source[index..];
            foreach (var candidate in TwoCharacterTokens)
            {
                if (!remaining.StartsWith(candidate.Symbol, StringComparison.Ordinal))
                    continue;

                index += 2;
                return new Token(candidate.Kind, candidate.Symbol);
            }

            var c = source[index++];
            return c switch
            {
                '?' => new Token(TokenKind.Question, "?"),
                ':' => new Token(TokenKind.Colon, ":"),
                '(' => new Token(TokenKind.LeftParen, "("),
                ')' => new Token(TokenKind.RightParen, ")"),
                ',' => new Token(TokenKind.Comma, ","),
                '+' => new Token(TokenKind.Plus, "+"),
                '-' => new Token(TokenKind.Minus, "-"),
                '*' => new Token(TokenKind.Star, "*"),
                '/' => new Token(TokenKind.Slash, "/"),
                '!' => new Token(TokenKind.Bang, "!"),
                '>' => new Token(TokenKind.Greater, ">"),
                '<' => new Token(TokenKind.Less, "<"),
                _ => throw new ParseException($"Unsupported character '{c}'.")
            };
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '-';

        private static readonly (string Symbol, TokenKind Kind)[] TwoCharacterTokens =
        [
            ("??", TokenKind.QuestionQuestion),
            ("&&", TokenKind.AndAnd),
            ("||", TokenKind.OrOr),
            ("==", TokenKind.EqualEqual),
            ("!=", TokenKind.BangEqual),
            (">=", TokenKind.GreaterOrEqual),
            ("<=", TokenKind.LessOrEqual)
        ];
    }

    private readonly record struct Token(TokenKind Kind, string Value);

    private enum TokenKind
    {
        End,
        Identifier,
        String,
        Number,
        Question,
        Colon,
        QuestionQuestion,
        AndAnd,
        OrOr,
        EqualEqual,
        BangEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        LeftParen,
        RightParen,
        Comma,
        Plus,
        Minus,
        Star,
        Slash,
        Bang
    }

    private sealed class ParseException(string message) : Exception(message);
}

public enum StencilExpressionNodeKind
{
    Literal,
    Property,
    SizeWidth,
    SizeHeight,
    RepeatIndex,
    Unary,
    Binary,
    Coalesce,
    Conditional,
    Map,
    Token
}

public enum StencilExpressionOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    And,
    Or,
    Not,
    Negate,
    Coalesce
}

public sealed class StencilExpressionNode
{
    private StencilExpressionNode(StencilExpressionNodeKind kind)
    {
        Kind = kind;
    }

    public StencilExpressionNodeKind Kind { get; }

    public string? Name { get; private init; }

    public object? Value { get; private init; }

    public StencilExpressionOperator? Operator { get; private init; }

    public StencilExpressionNode? Left { get; private init; }

    public StencilExpressionNode? Right { get; private init; }

    public StencilExpressionNode? Operand { get; private init; }

    public StencilExpressionNode? Condition { get; private init; }

    public StencilExpressionNode? WhenTrue { get; private init; }

    public StencilExpressionNode? WhenFalse { get; private init; }

    public StencilExpressionNode? Source { get; private init; }

    public IReadOnlyDictionary<string, StencilExpressionNode> MapEntries { get; private init; }
        = new Dictionary<string, StencilExpressionNode>();

    public StencilExpressionNode? Default { get; private init; }

    public static StencilExpressionNode Literal(object? value)
        => new(StencilExpressionNodeKind.Literal) { Value = value };

    public static StencilExpressionNode Null() => Literal(null);

    public static StencilExpressionNode Property(string name)
        => new(StencilExpressionNodeKind.Property) { Name = name };

    public static StencilExpressionNode Simple(StencilExpressionNodeKind kind) => new(kind);

    public static StencilExpressionNode Unary(StencilExpressionOperator op, StencilExpressionNode operand)
        => new(StencilExpressionNodeKind.Unary) { Operator = op, Operand = operand };

    public static StencilExpressionNode Binary(
        StencilExpressionOperator op,
        StencilExpressionNode left,
        StencilExpressionNode right)
    {
        var kind = op == StencilExpressionOperator.Coalesce
            ? StencilExpressionNodeKind.Coalesce
            : StencilExpressionNodeKind.Binary;

        return new StencilExpressionNode(kind)
        {
            Operator = op,
            Left = left,
            Right = right
        };
    }

    public static StencilExpressionNode Conditional(
        StencilExpressionNode condition,
        StencilExpressionNode whenTrue,
        StencilExpressionNode whenFalse)
        => new(StencilExpressionNodeKind.Conditional)
        {
            Condition = condition,
            WhenTrue = whenTrue,
            WhenFalse = whenFalse
        };

    public static StencilExpressionNode Map(
        StencilExpressionNode source,
        IReadOnlyDictionary<string, StencilExpressionNode> entries,
        StencilExpressionNode defaultNode)
        => new(StencilExpressionNodeKind.Map)
        {
            Source = source,
            MapEntries = entries,
            Default = defaultNode
        };

    public static StencilExpressionNode Token(string key, StencilExpressionNode? fallback)
        => new(StencilExpressionNodeKind.Token)
        {
            Name = key,
            Default = fallback
        };
}
