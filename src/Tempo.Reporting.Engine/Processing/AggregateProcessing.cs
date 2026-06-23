#pragma warning disable MA0048

using System.Globalization;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Evaluates processing-time aggregate expressions over materialized rows.</summary>
public static class ReportAggregateEngine
{
    /// <summary>Evaluates an expression, resolving group/report aggregates and deferring page aggregates.</summary>
    public static ExpressionValue Evaluate(
        string expression,
        IReadOnlyList<ProcessedDataRow> rows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow>? reportRows = null)
        => Evaluate(ExpressionParser.Parse(expression), rows, context, reportRows);

    /// <summary>Evaluates a parsed expression, resolving group/report aggregates and deferring page aggregates.</summary>
    public static ExpressionValue Evaluate(
        ExpressionNode expression,
        IReadOnlyList<ProcessedDataRow> rows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow>? reportRows = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(context);
        using var _ = new CultureScope(context.Culture);
        return EvaluateNode(expression, rows, rows.FirstOrDefault(), context, reportRows ?? rows);
    }

    /// <summary>Evaluates a running aggregate for each row prefix.</summary>
    public static IReadOnlyList<ExpressionValue> EvaluateRunningTotal(
        string aggregateExpression,
        IReadOnlyList<ProcessedDataRow> rows,
        ReportProcessingContext context)
    {
        var expression = ExpressionParser.Parse(aggregateExpression);
        var values = new List<ExpressionValue>(rows.Count);
        for (var i = 1; i <= rows.Count; i++)
        {
            values.Add(Evaluate(expression, rows.Take(i).ToArray(), context, rows));
        }

        return values;
    }

    internal static ExpressionValue EvaluateForRow(
        string expression,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow>? reportRows = null)
        => EvaluateForRow(ExpressionParser.Parse(expression), row, scopeRows, context, reportRows);

    internal static ExpressionValue EvaluateForRow(
        ExpressionNode expression,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow>? reportRows = null)
    {
        using var _ = new CultureScope(context.Culture);
        return EvaluateNode(expression, scopeRows, row, context, reportRows ?? scopeRows);
    }

    private static ExpressionValue EvaluateNode(
        ExpressionNode node,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ProcessedDataRow? currentRow,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        return node switch
        {
            AggregateExpressionNode aggregate => EvaluateAggregate(aggregate, scopeRows, context, reportRows),
            UnaryExpressionNode unary => EvaluateUnary(unary, scopeRows, currentRow, context, reportRows),
            BinaryExpressionNode binary => EvaluateBinary(binary, scopeRows, currentRow, context, reportRows),
            FunctionCallExpressionNode call when ContainsAggregate(call) => EvaluateAggregateAwareFunction(
                call,
                scopeRows,
                currentRow,
                context,
                reportRows),
            _ => ExpressionEvaluator.Evaluate(node, context.CreateExpressionContext(currentRow)),
        };
    }

    private static ExpressionValue EvaluateUnary(
        UnaryExpressionNode unary,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ProcessedDataRow? currentRow,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        var value = EvaluateNode(unary.Operand, scopeRows, currentRow, context, reportRows);
        if (value.Kind == ExpressionValueKind.Null)
        {
            return ExpressionValue.Null;
        }

        return unary.Operator switch
        {
            ExpressionUnaryOperator.Negate => ExpressionValue.Number(-value.AsNumber()),
            ExpressionUnaryOperator.Not => ExpressionValue.Boolean(!value.AsBoolean()),
            _ => ExpressionValue.Null,
        };
    }

    private static ExpressionValue EvaluateBinary(
        BinaryExpressionNode binary,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ProcessedDataRow? currentRow,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        if (binary.Operator == ExpressionBinaryOperator.And)
        {
            var left = EvaluateNode(binary.Left, scopeRows, currentRow, context, reportRows);
            return !left.AsBoolean()
                ? ExpressionValue.Boolean(false)
                : ExpressionValue.Boolean(EvaluateNode(binary.Right, scopeRows, currentRow, context, reportRows).AsBoolean());
        }

        if (binary.Operator == ExpressionBinaryOperator.Or)
        {
            var left = EvaluateNode(binary.Left, scopeRows, currentRow, context, reportRows);
            return left.AsBoolean()
                ? ExpressionValue.Boolean(true)
                : ExpressionValue.Boolean(EvaluateNode(binary.Right, scopeRows, currentRow, context, reportRows).AsBoolean());
        }

        var l = EvaluateNode(binary.Left, scopeRows, currentRow, context, reportRows);
        var r = EvaluateNode(binary.Right, scopeRows, currentRow, context, reportRows);

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
            ExpressionBinaryOperator.LessThan => ExpressionValue.Boolean(ReportValueComparer.Compare(l, r, context) < 0),
            ExpressionBinaryOperator.LessThanOrEqual => ExpressionValue.Boolean(ReportValueComparer.Compare(l, r, context) <= 0),
            ExpressionBinaryOperator.GreaterThan => ExpressionValue.Boolean(ReportValueComparer.Compare(l, r, context) > 0),
            ExpressionBinaryOperator.GreaterThanOrEqual => ExpressionValue.Boolean(ReportValueComparer.Compare(l, r, context) >= 0),
            _ => ExpressionValue.Null,
        };
    }

    private static ExpressionValue EvaluateAggregateAwareFunction(
        FunctionCallExpressionNode call,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ProcessedDataRow? currentRow,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        if (string.Equals(call.Name, "IIf", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count == 3)
        {
            return EvaluateNode(call.Arguments[0], scopeRows, currentRow, context, reportRows).AsBoolean()
                ? EvaluateNode(call.Arguments[1], scopeRows, currentRow, context, reportRows)
                : EvaluateNode(call.Arguments[2], scopeRows, currentRow, context, reportRows);
        }

        if (string.Equals(call.Name, "IsNull", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count is 1 or 2)
        {
            var value = EvaluateNode(call.Arguments[0], scopeRows, currentRow, context, reportRows);
            return call.Arguments.Count == 1
                ? ExpressionValue.Boolean(value.Kind == ExpressionValueKind.Null)
                : value.Kind == ExpressionValueKind.Null
                    ? EvaluateNode(call.Arguments[1], scopeRows, currentRow, context, reportRows)
                    : value;
        }

        return ExpressionEvaluator.Evaluate(call, context.CreateExpressionContext(currentRow));
    }

    private static ExpressionValue EvaluateAggregate(
        AggregateExpressionNode aggregate,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
    {
        if (aggregate.Scope == ReportAggregateScope.Page)
        {
            return ExpressionValue.Deferred(ExpressionDeferredKind.PageAggregate);
        }

        var rows = aggregate.Scope == ReportAggregateScope.Report ? reportRows : scopeRows;
        var values = rows
            .Select(row => aggregate.ValueExpression is null
                ? ExpressionValue.Number(1m)
                : ExpressionEvaluator.Evaluate(aggregate.ValueExpression, context.CreateExpressionContext(row)))
            .ToArray();

        return aggregate.Aggregate switch
        {
            ReportAggregateFunction.Sum => ExpressionValue.Number(values.Sum(value => value.AsNumber())),
            ReportAggregateFunction.Count => ExpressionValue.Number(values.Count(value => value.Kind != ExpressionValueKind.Null)),
            ReportAggregateFunction.CountDistinct => ExpressionValue.Number(values
                .Where(value => value.Kind != ExpressionValueKind.Null)
                .Select(value => new AggregateValueKey(value))
                .Distinct()
                .Count()),
            ReportAggregateFunction.Min => MinMax(values, context, min: true),
            ReportAggregateFunction.Max => MinMax(values, context, min: false),
            ReportAggregateFunction.Avg => Average(values),
            ReportAggregateFunction.First => values.FirstOrDefault(value => value.Kind != ExpressionValueKind.Null) ?? ExpressionValue.Null,
            ReportAggregateFunction.Last => values.LastOrDefault(value => value.Kind != ExpressionValueKind.Null) ?? ExpressionValue.Null,
            _ => ExpressionValue.Null,
        };
    }

    private static ExpressionValue Average(IReadOnlyList<ExpressionValue> values)
    {
        var numeric = values.Where(value => value.Kind != ExpressionValueKind.Null).ToArray();
        return numeric.Length == 0
            ? ExpressionValue.Null
            : ExpressionValue.Number(numeric.Sum(value => value.AsNumber()) / numeric.Length);
    }

    private static ExpressionValue MinMax(
        IReadOnlyList<ExpressionValue> values,
        ReportProcessingContext context,
        bool min)
    {
        var current = values.FirstOrDefault(value => value.Kind != ExpressionValueKind.Null);
        if (current is null)
        {
            return ExpressionValue.Null;
        }

        foreach (var value in values.Where(value => value.Kind != ExpressionValueKind.Null).Skip(1))
        {
            var comparison = ReportValueComparer.Compare(value, current, context);
            if ((min && comparison < 0) || (!min && comparison > 0))
            {
                current = value;
            }
        }

        return current;
    }

    private static bool ContainsAggregate(ExpressionNode node)
    {
        return node switch
        {
            AggregateExpressionNode => true,
            UnaryExpressionNode unary => ContainsAggregate(unary.Operand),
            BinaryExpressionNode binary => ContainsAggregate(binary.Left) || ContainsAggregate(binary.Right),
            FunctionCallExpressionNode call => call.Arguments.Any(ContainsAggregate),
            MemberAccessExpressionNode member => member.Target is not null && ContainsAggregate(member.Target),
            _ => false,
        };
    }

    private static bool ValuesEqual(ExpressionValue left, ExpressionValue right)
        => left.Kind == right.Kind && Equals(left.RawValue, right.RawValue);

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

    private readonly record struct AggregateValueKey(ExpressionValue Value)
    {
        public override string ToString() => $"{Value.Kind}:{Value.AsString()}";
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        public CultureScope(CultureInfo culture)
        {
            _culture = CultureInfo.CurrentCulture;
            _uiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}

#pragma warning restore MA0048
