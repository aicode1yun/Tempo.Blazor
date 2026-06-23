namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Parser for report expressions.</summary>
public static class ExpressionParser
{
    /// <summary>Parses an expression into an AST.</summary>
    public static ExpressionNode Parse(string expression, ExpressionParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        options ??= new ExpressionParseOptions();
        if (expression.Length > options.MaxExpressionLength)
        {
            throw new ExpressionParseException(ExpressionDiagnostics.Create(
                "ExpressionParser.ExpressionTooLong",
                1,
                1,
                options.MaxExpressionLength));
        }

        return new Parser(ExpressionLexer.Tokenize(expression), options).Parse();
    }

    private sealed class Parser
    {
        private static readonly Dictionary<string, ReportAggregateFunction> AggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Sum"] = ReportAggregateFunction.Sum,
            ["Count"] = ReportAggregateFunction.Count,
            ["CountDistinct"] = ReportAggregateFunction.CountDistinct,
            ["Min"] = ReportAggregateFunction.Min,
            ["Max"] = ReportAggregateFunction.Max,
            ["Avg"] = ReportAggregateFunction.Avg,
            ["First"] = ReportAggregateFunction.First,
            ["Last"] = ReportAggregateFunction.Last,
        };

        private readonly IReadOnlyList<ExpressionToken> _tokens;
        private readonly ExpressionParseOptions _options;
        private int _position;

        public Parser(IReadOnlyList<ExpressionToken> tokens, ExpressionParseOptions options)
        {
            _tokens = tokens;
            _options = options;
        }

        public ExpressionNode Parse()
        {
            var expression = ParseOr(0);
            Expect(ExpressionTokenKind.EndOfInput);
            return expression;
        }

        private ExpressionNode ParseOr(int depth)
        {
            var left = ParseAnd(depth);
            while (MatchKeyword("or"))
            {
                left = new BinaryExpressionNode(ExpressionBinaryOperator.Or, left, ParseAnd(depth));
            }

            return left;
        }

        private ExpressionNode ParseAnd(int depth)
        {
            var left = ParseComparison(depth);
            while (MatchKeyword("and"))
            {
                left = new BinaryExpressionNode(ExpressionBinaryOperator.And, left, ParseComparison(depth));
            }

            return left;
        }

        private ExpressionNode ParseComparison(int depth)
        {
            var left = ParseAdditive(depth);
            while (true)
            {
                var op = Current.Kind switch
                {
                    ExpressionTokenKind.Equal => ExpressionBinaryOperator.Equal,
                    ExpressionTokenKind.NotEqual => ExpressionBinaryOperator.NotEqual,
                    ExpressionTokenKind.Less => ExpressionBinaryOperator.LessThan,
                    ExpressionTokenKind.LessOrEqual => ExpressionBinaryOperator.LessThanOrEqual,
                    ExpressionTokenKind.Greater => ExpressionBinaryOperator.GreaterThan,
                    ExpressionTokenKind.GreaterOrEqual => ExpressionBinaryOperator.GreaterThanOrEqual,
                    _ => (ExpressionBinaryOperator?)null,
                };

                if (op is null)
                {
                    return left;
                }

                Advance();
                left = new BinaryExpressionNode(op.Value, left, ParseAdditive(depth));
            }
        }

        private ExpressionNode ParseAdditive(int depth)
        {
            var left = ParseMultiplicative(depth);
            while (Current.Kind is ExpressionTokenKind.Plus or ExpressionTokenKind.Minus)
            {
                var op = Current.Kind == ExpressionTokenKind.Plus
                    ? ExpressionBinaryOperator.Add
                    : ExpressionBinaryOperator.Subtract;
                Advance();
                left = new BinaryExpressionNode(op, left, ParseMultiplicative(depth));
            }

            return left;
        }

        private ExpressionNode ParseMultiplicative(int depth)
        {
            var left = ParseUnary(depth);
            while (Current.Kind is ExpressionTokenKind.Star or ExpressionTokenKind.Slash or ExpressionTokenKind.Percent)
            {
                var op = Current.Kind switch
                {
                    ExpressionTokenKind.Star => ExpressionBinaryOperator.Multiply,
                    ExpressionTokenKind.Slash => ExpressionBinaryOperator.Divide,
                    _ => ExpressionBinaryOperator.Modulo,
                };
                Advance();
                left = new BinaryExpressionNode(op, left, ParseUnary(depth));
            }

            return left;
        }

        private ExpressionNode ParseUnary(int depth)
        {
            if (Current.Kind == ExpressionTokenKind.Minus)
            {
                Advance();
                return new UnaryExpressionNode(ExpressionUnaryOperator.Negate, ParseUnary(depth));
            }

            if (MatchKeyword("not"))
            {
                return new UnaryExpressionNode(ExpressionUnaryOperator.Not, ParseUnary(depth));
            }

            return ParsePostfix(depth);
        }

        private ExpressionNode ParsePostfix(int depth)
        {
            var expression = ParsePrimary(depth);
            while (Current.Kind == ExpressionTokenKind.Dot)
            {
                Advance();
                var member = Expect(ExpressionTokenKind.Identifier);
                if (expression is MemberAccessExpressionNode { Target: null } existing)
                {
                    expression = existing with { Path = existing.Path.Concat([member.Text]).ToArray() };
                }
                else
                {
                    expression = new MemberAccessExpressionNode([member.Text], expression);
                }
            }

            return expression;
        }

        private ExpressionNode ParsePrimary(int depth)
        {
            if (Current.Kind == ExpressionTokenKind.Number)
            {
                var token = Advance();
                return new LiteralExpressionNode(ExpressionValue.Number((decimal)token.Value!));
            }

            if (Current.Kind == ExpressionTokenKind.String)
            {
                var token = Advance();
                return new LiteralExpressionNode(ExpressionValue.String((string)token.Value!));
            }

            if (Current.Kind == ExpressionTokenKind.Identifier)
            {
                return ParseIdentifier();
            }

            if (Current.Kind == ExpressionTokenKind.OpenParen)
            {
                if (depth + 1 > _options.MaxDepth)
                {
                    throw Error("ExpressionParser.ExpressionTooDeep", Current, _options.MaxDepth);
                }

                Advance();
                var expression = ParseOr(depth + 1);
                Expect(ExpressionTokenKind.CloseParen);
                return expression;
            }

            throw Error("ExpressionParser.UnexpectedToken", Current, Current.Text);
        }

        private ExpressionNode ParseIdentifier()
        {
            var identifier = Advance();

            if (Current.Kind != ExpressionTokenKind.OpenParen)
            {
                if (StringEquals(identifier.Text, "true"))
                {
                    return new LiteralExpressionNode(ExpressionValue.Boolean(true));
                }

                if (StringEquals(identifier.Text, "false"))
                {
                    return new LiteralExpressionNode(ExpressionValue.Boolean(false));
                }

                if (StringEquals(identifier.Text, "null"))
                {
                    return new LiteralExpressionNode(ExpressionValue.Null);
                }

                return new MemberAccessExpressionNode([identifier.Text]);
            }

            Advance();
            var args = new List<ExpressionNode>();
            if (Current.Kind != ExpressionTokenKind.CloseParen)
            {
                do
                {
                    args.Add(ParseOr(0));
                }
                while (Match(ExpressionTokenKind.Comma));
            }

            Expect(ExpressionTokenKind.CloseParen);

            if (AggregateFunctions.TryGetValue(identifier.Text, out var aggregate))
            {
                if (!_options.AllowAggregates)
                {
                    throw Error("ExpressionParser.AggregateNotAllowed", identifier, identifier.Text);
                }

                var scope = ParseAggregateScope(args);
                var value = args.Count == 0 ? null : args[0];
                return new AggregateExpressionNode(aggregate, value, scope);
            }

            return new FunctionCallExpressionNode(identifier.Text, args);
        }

        private static ReportAggregateScope ParseAggregateScope(IReadOnlyList<ExpressionNode> args)
        {
            if (args.Count < 2)
            {
                return ReportAggregateScope.Group;
            }

            if (args[1] is not LiteralExpressionNode { Value.Kind: ExpressionValueKind.String } scopeLiteral)
            {
                return ReportAggregateScope.Group;
            }

            return scopeLiteral.Value.AsString().ToLowerInvariant() switch
            {
                "page" => ReportAggregateScope.Page,
                "report" => ReportAggregateScope.Report,
                _ => ReportAggregateScope.Group,
            };
        }

        private bool Match(ExpressionTokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private bool MatchKeyword(string keyword)
        {
            if (Current.Kind == ExpressionTokenKind.Identifier && StringEquals(Current.Text, keyword))
            {
                Advance();
                return true;
            }

            return false;
        }

        private ExpressionToken Expect(ExpressionTokenKind kind)
        {
            if (Current.Kind == kind)
            {
                return Advance();
            }

            var token = Current.Kind == ExpressionTokenKind.EndOfInput && _position > 0
                ? Current with { Column = Math.Max(1, Current.Column - 1) }
                : Current;
            throw Error("ExpressionParser.ExpectedToken", token, kind.ToString());
        }

        private ExpressionToken Current => _tokens[_position];

        private ExpressionToken Advance() => _tokens[_position++];

        private ExpressionParseException Error(string code, ExpressionToken token, params object?[] args)
            => new(ExpressionDiagnostics.Create(code, token.Line, token.Column, args));

        private static bool StringEquals(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
