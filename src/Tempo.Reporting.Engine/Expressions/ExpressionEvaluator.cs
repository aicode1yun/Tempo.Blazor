using System.Diagnostics;
using System.Globalization;

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Safe evaluator for report expressions.</summary>
public static class ExpressionEvaluator
{
    /// <summary>Parses and evaluates an expression.</summary>
    public static ExpressionValue Evaluate(
        string expression,
        ExpressionContext context,
        ExpressionEvaluationOptions? options = null)
        => Evaluate(ExpressionParser.Parse(expression), context, options);

    /// <summary>Evaluates a parsed expression.</summary>
    public static ExpressionValue Evaluate(
        ExpressionNode expression,
        ExpressionContext context,
        ExpressionEvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);
        return new Evaluator(context, options ?? new ExpressionEvaluationOptions()).Evaluate(expression);
    }

    private sealed class Evaluator
    {
        private readonly ExpressionContext _context;
        private readonly ExpressionEvaluationOptions _options;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _steps;

        public Evaluator(ExpressionContext context, ExpressionEvaluationOptions options)
        {
            _context = context;
            _options = options;
        }

        public ExpressionValue Evaluate(ExpressionNode node)
        {
            Step(node);
            return node switch
            {
                LiteralExpressionNode literal => literal.Value,
                MemberAccessExpressionNode member => EvaluateMember(member),
                UnaryExpressionNode unary => EvaluateUnary(unary),
                BinaryExpressionNode binary => EvaluateBinary(binary),
                FunctionCallExpressionNode call => EvaluateFunction(call),
                AggregateExpressionNode aggregate => throw Error("ExpressionEvaluator.AggregateRequiresProcessing", aggregate),
                _ => throw Error("ExpressionEvaluator.UnsupportedNode", node),
            };
        }

        private ExpressionValue EvaluateUnary(UnaryExpressionNode unary)
        {
            var value = Evaluate(unary.Operand);
            if (value.Kind == ExpressionValueKind.Null)
            {
                return ExpressionValue.Null;
            }

            return unary.Operator switch
            {
                ExpressionUnaryOperator.Negate => ExpressionValue.Number(-value.AsNumber()),
                ExpressionUnaryOperator.Not => ExpressionValue.Boolean(!value.AsBoolean()),
                _ => throw Error("ExpressionEvaluator.UnsupportedOperator", unary),
            };
        }

        private ExpressionValue EvaluateBinary(BinaryExpressionNode binary)
        {
            if (binary.Operator == ExpressionBinaryOperator.And)
            {
                var left = Evaluate(binary.Left);
                return !left.AsBoolean()
                    ? ExpressionValue.Boolean(false)
                    : ExpressionValue.Boolean(Evaluate(binary.Right).AsBoolean());
            }

            if (binary.Operator == ExpressionBinaryOperator.Or)
            {
                var left = Evaluate(binary.Left);
                return left.AsBoolean()
                    ? ExpressionValue.Boolean(true)
                    : ExpressionValue.Boolean(Evaluate(binary.Right).AsBoolean());
            }

            var l = Evaluate(binary.Left);
            var r = Evaluate(binary.Right);

            if (binary.Operator is ExpressionBinaryOperator.Equal or ExpressionBinaryOperator.NotEqual)
            {
                var equals = ValuesEqual(l, r);
                return ExpressionValue.Boolean(binary.Operator == ExpressionBinaryOperator.Equal ? equals : !equals);
            }

            if (binary.Operator == ExpressionBinaryOperator.Add &&
                (l.Kind == ExpressionValueKind.String || r.Kind == ExpressionValueKind.String) &&
                !CanCoerceBothToNumber(l, r))
            {
                return ExpressionValue.String(l.AsString() + r.AsString());
            }

            if (l.Kind == ExpressionValueKind.Null || r.Kind == ExpressionValueKind.Null)
            {
                return ExpressionValue.Null;
            }

            return binary.Operator switch
            {
                ExpressionBinaryOperator.Add => ExpressionValue.Number(l.AsNumber() + r.AsNumber()),
                ExpressionBinaryOperator.Subtract => ExpressionValue.Number(l.AsNumber() - r.AsNumber()),
                ExpressionBinaryOperator.Multiply => ExpressionValue.Number(l.AsNumber() * r.AsNumber()),
                ExpressionBinaryOperator.Divide => ExpressionValue.Number(l.AsNumber() / r.AsNumber()),
                ExpressionBinaryOperator.Modulo => ExpressionValue.Number(l.AsNumber() % r.AsNumber()),
                ExpressionBinaryOperator.LessThan => ExpressionValue.Boolean(Compare(l, r) < 0),
                ExpressionBinaryOperator.LessThanOrEqual => ExpressionValue.Boolean(Compare(l, r) <= 0),
                ExpressionBinaryOperator.GreaterThan => ExpressionValue.Boolean(Compare(l, r) > 0),
                ExpressionBinaryOperator.GreaterThanOrEqual => ExpressionValue.Boolean(Compare(l, r) >= 0),
                _ => throw Error("ExpressionEvaluator.UnsupportedOperator", binary),
            };
        }

        private ExpressionValue EvaluateMember(MemberAccessExpressionNode member)
        {
            if (member.Target is not null)
            {
                return EvaluateTargetMember(Evaluate(member.Target), member.Path);
            }

            if (member.Path.Count == 0)
            {
                throw Error("ExpressionEvaluator.UnknownRoot", member);
            }

            var root = member.Path[0];
            if (StringEquals(root, "Fields"))
            {
                return ResolveDictionary(_context.Fields, member, "Fields");
            }

            if (StringEquals(root, "Parameters"))
            {
                return ResolveDictionary(_context.Parameters, member, "Parameters");
            }

            if (StringEquals(root, "Globals"))
            {
                return ResolveGlobal(member);
            }

            throw Error("ExpressionEvaluator.UnknownRoot", member, root);
        }

        private ExpressionValue ResolveDictionary(
            IReadOnlyDictionary<string, object?> values,
            MemberAccessExpressionNode member,
            string root)
        {
            if (member.Path.Count != 2 || !values.TryGetValue(member.Path[1], out var value))
            {
                throw Error("ExpressionEvaluator.UnknownMember", member, string.Join(".", member.Path));
            }

            return ExpressionValue.FromObject(value);
        }

        private ExpressionValue ResolveGlobal(MemberAccessExpressionNode member)
        {
            if (member.Path.Count != 2)
            {
                throw Error("ExpressionEvaluator.UnknownMember", member, string.Join(".", member.Path));
            }

            return member.Path[1].ToLowerInvariant() switch
            {
                "pagenumber" => ExpressionValue.Deferred(ExpressionDeferredKind.PageNumber),
                "totalpages" => ExpressionValue.Deferred(ExpressionDeferredKind.TotalPages),
                "executiontime" => ExpressionValue.Date(_context.Globals.ExecutionTime),
                "username" => ExpressionValue.String(_context.Globals.UserName),
                "tenantname" => ExpressionValue.String(_context.Globals.TenantName),
                _ => throw Error("ExpressionEvaluator.UnknownMember", member, string.Join(".", member.Path)),
            };
        }

        private ExpressionValue EvaluateTargetMember(ExpressionValue target, IReadOnlyList<string> path)
        {
            var current = target;
            foreach (var member in path)
            {
                if (current.Kind == ExpressionValueKind.Date)
                {
                    var date = current.AsDate();
                    current = member.ToLowerInvariant() switch
                    {
                        "year" => ExpressionValue.Number(date.Year),
                        "month" => ExpressionValue.Number(date.Month),
                        "day" => ExpressionValue.Number(date.Day),
                        "hour" => ExpressionValue.Number(date.Hour),
                        "minute" => ExpressionValue.Number(date.Minute),
                        "second" => ExpressionValue.Number(date.Second),
                        _ => throw Error("ExpressionEvaluator.UnknownMember", new LiteralExpressionNode(current), member),
                    };
                    continue;
                }

                if (current.Kind == ExpressionValueKind.String && StringEquals(member, "Length"))
                {
                    current = ExpressionValue.Number(current.AsString().Length);
                    continue;
                }

                throw Error("ExpressionEvaluator.UnknownMember", new LiteralExpressionNode(current), member);
            }

            return current;
        }

        private ExpressionValue EvaluateFunction(FunctionCallExpressionNode call)
        {
            var name = call.Name.ToLowerInvariant();

            if (StringEquals(name, "iif"))
            {
                RequireArgumentCount(call, 3);
                return Evaluate(call.Arguments[0]).AsBoolean()
                    ? Evaluate(call.Arguments[1])
                    : Evaluate(call.Arguments[2]);
            }

            if (StringEquals(name, "switch"))
            {
                return EvaluateSwitch(call);
            }

            if (StringEquals(name, "isnull"))
            {
                RequireArgumentCount(call, min: 1, max: 2);
                var value = Evaluate(call.Arguments[0]);
                if (call.Arguments.Count == 1)
                {
                    return ExpressionValue.Boolean(value.Kind == ExpressionValueKind.Null);
                }

                return value.Kind == ExpressionValueKind.Null ? Evaluate(call.Arguments[1]) : value;
            }

            var args = call.Arguments.Select(Evaluate).ToArray();
            return name switch
            {
                "abs" => ExpressionValue.Number(Math.Abs(NumberArg(args, 0))),
                "round" => ExpressionValue.Number(Math.Round(NumberArg(args, 0), args.Length > 1 ? (int)NumberArg(args, 1) : 0)),
                "floor" => ExpressionValue.Number(Math.Floor(NumberArg(args, 0))),
                "ceiling" or "ceil" => ExpressionValue.Number(Math.Ceiling(NumberArg(args, 0))),
                "trim" => ExpressionValue.String(StringArg(args, 0).Trim()),
                "upper" => ExpressionValue.String(StringArg(args, 0).ToUpper(CultureInfo.CurrentCulture)),
                "lower" => ExpressionValue.String(StringArg(args, 0).ToLower(CultureInfo.CurrentCulture)),
                "length" => ExpressionValue.Number(StringArg(args, 0).Length),
                "format" => Format(args),
                "year" => ExpressionValue.Number(DateArg(args, 0).Year),
                "month" => ExpressionValue.Number(DateArg(args, 0).Month),
                "day" => ExpressionValue.Number(DateArg(args, 0).Day),
                "adddays" => ExpressionValue.Date(DateArg(args, 0).AddDays((double)NumberArg(args, 1))),
                "cstr" or "tostring" => ExpressionValue.String(args[0].AsString()),
                "cdec" or "tonumber" => ExpressionValue.Number(args[0].AsNumber()),
                "cbool" or "toboolean" => ExpressionValue.Boolean(args[0].AsBoolean()),
                "cdate" or "todate" => ExpressionValue.Date(args[0].AsDate()),
                _ => throw Error("ExpressionEvaluator.UnknownFunction", call, call.Name),
            };
        }

        private ExpressionValue EvaluateSwitch(FunctionCallExpressionNode call)
        {
            if (call.Arguments.Count == 0)
            {
                return ExpressionValue.Null;
            }

            var i = 0;
            while (i + 1 < call.Arguments.Count)
            {
                if (Evaluate(call.Arguments[i]).AsBoolean())
                {
                    return Evaluate(call.Arguments[i + 1]);
                }

                i += 2;
            }

            return i < call.Arguments.Count ? Evaluate(call.Arguments[i]) : ExpressionValue.Null;
        }

        private static ExpressionValue Format(ExpressionValue[] args)
        {
            var format = StringArg(args, 1);
            var value = args[0];
            return value.Kind switch
            {
                ExpressionValueKind.Date => ExpressionValue.String(value.AsDate().ToString(format, CultureInfo.CurrentCulture)),
                ExpressionValueKind.Number => ExpressionValue.String(value.AsNumber().ToString(format, CultureInfo.CurrentCulture)),
                _ => ExpressionValue.String(value.AsString()),
            };
        }

        private static decimal NumberArg(ExpressionValue[] args, int index) => args[index].AsNumber();

        private static string StringArg(ExpressionValue[] args, int index) => args[index].AsString();

        private static DateTimeOffset DateArg(ExpressionValue[] args, int index) => args[index].AsDate();

        private void RequireArgumentCount(FunctionCallExpressionNode call, int min, int? max = null)
        {
            if (call.Arguments.Count < min || (max is not null && call.Arguments.Count > max))
            {
                throw Error("ExpressionEvaluator.InvalidArgumentCount", call, call.Name);
            }
        }

        private void RequireArgumentCount(FunctionCallExpressionNode call, int count)
            => RequireArgumentCount(call, count, count);

        private static bool ValuesEqual(ExpressionValue left, ExpressionValue right)
        {
            if (left.Kind == ExpressionValueKind.Null || right.Kind == ExpressionValueKind.Null)
            {
                return left.Kind == right.Kind;
            }

            if (left.Kind == ExpressionValueKind.String || right.Kind == ExpressionValueKind.String)
            {
                return string.Equals(left.AsString(), right.AsString(), StringComparison.Ordinal);
            }

            if (left.Kind == ExpressionValueKind.Boolean || right.Kind == ExpressionValueKind.Boolean)
            {
                return left.AsBoolean() == right.AsBoolean();
            }

            if (left.Kind == ExpressionValueKind.Date || right.Kind == ExpressionValueKind.Date)
            {
                return left.AsDate().Equals(right.AsDate());
            }

            return left.AsNumber() == right.AsNumber();
        }

        private static int Compare(ExpressionValue left, ExpressionValue right)
        {
            if (left.Kind == ExpressionValueKind.String || right.Kind == ExpressionValueKind.String)
            {
                return string.Compare(left.AsString(), right.AsString(), StringComparison.CurrentCulture);
            }

            if (left.Kind == ExpressionValueKind.Date || right.Kind == ExpressionValueKind.Date)
            {
                return left.AsDate().CompareTo(right.AsDate());
            }

            return left.AsNumber().CompareTo(right.AsNumber());
        }

        private static bool CanCoerceBothToNumber(ExpressionValue left, ExpressionValue right)
        {
            try
            {
                _ = left.AsNumber();
                _ = right.AsNumber();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void Step(ExpressionNode node)
        {
            _steps++;
            if (_steps > _options.MaxEvaluationSteps || _stopwatch.Elapsed > _options.Timeout)
            {
                throw Error("ExpressionEvaluator.Timeout", node);
            }
        }

        private ExpressionEvaluationException Error(string code, ExpressionNode node, params object?[] args)
            => new(ExpressionDiagnostics.Create(code, 1, 1, args));

        private static bool StringEquals(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
