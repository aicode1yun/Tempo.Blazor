using System.Globalization;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Evaluates safe stencil expressions. Public evaluation APIs never throw.</summary>
public sealed class StencilEvaluator
{
    private const int MaxEvaluationDepth = 256;

    /// <summary>Parses and evaluates <paramref name="expression"/>; malformed input returns the raw string and never throws.</summary>
    public StencilValue Evaluate(string? expression, StencilEvalContext? context = null)
        => Evaluate(StencilExpression.Parse(expression), context);

    /// <summary>Evaluates <paramref name="expression"/>; malformed input returns the raw string and never throws.</summary>
    public StencilValue Evaluate(StencilExpression expression, StencilEvalContext? context = null)
    {
        try
        {
            if (expression.IsMalformed)
                return new StencilValue(expression.Raw);

            return EvaluateNode(expression.Root, context ?? StencilEvalContext.Empty, 0);
        }
        catch
        {
            return new StencilValue(expression.Raw);
        }
    }

    private static StencilValue EvaluateNode(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        if (depth > MaxEvaluationDepth)
            throw new InvalidOperationException("Expression evaluation exceeds the maximum supported depth.");

        return node.Kind switch
        {
            StencilExpressionNodeKind.Literal => new StencilValue(node.Value),
            StencilExpressionNodeKind.Property => EvaluateProperty(node.Name, context),
            StencilExpressionNodeKind.SizeWidth => new StencilValue(context.SizeW),
            StencilExpressionNodeKind.SizeHeight => new StencilValue(context.SizeH),
            StencilExpressionNodeKind.RepeatIndex => new StencilValue(context.RepeatIndex),
            StencilExpressionNodeKind.Unary => EvaluateUnary(node, context, depth + 1),
            StencilExpressionNodeKind.Binary => EvaluateBinary(node, context, depth + 1),
            StencilExpressionNodeKind.Coalesce => EvaluateCoalesce(node, context, depth + 1),
            StencilExpressionNodeKind.Conditional => EvaluateNode(node.Condition!, context, depth + 1).AsBool()
                ? EvaluateNode(node.WhenTrue!, context, depth + 1)
                : EvaluateNode(node.WhenFalse!, context, depth + 1),
            StencilExpressionNodeKind.Map => EvaluateMap(node, context, depth + 1),
            StencilExpressionNodeKind.Token => EvaluateToken(node, context, depth + 1),
            _ => StencilValue.Null
        };
    }

    private static StencilValue EvaluateProperty(string? name, StencilEvalContext context)
    {
        if (string.IsNullOrEmpty(name))
            return StencilValue.Null;

        return context.Props.TryGetValue(name, out var value)
            ? new StencilValue(value)
            : StencilValue.Null;
    }

    private static StencilValue EvaluateUnary(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        var operand = EvaluateNode(node.Operand!, context, depth);
        return node.Operator switch
        {
            StencilExpressionOperator.Not => new StencilValue(!operand.AsBool()),
            StencilExpressionOperator.Negate => new StencilValue(-operand.AsDouble()),
            _ => StencilValue.Null
        };
    }

    private static StencilValue EvaluateBinary(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        if (node.Operator == StencilExpressionOperator.And)
        {
            var left = EvaluateNode(node.Left!, context, depth);
            return left.AsBool() ? new StencilValue(EvaluateNode(node.Right!, context, depth).AsBool()) : new StencilValue(false);
        }

        if (node.Operator == StencilExpressionOperator.Or)
        {
            var left = EvaluateNode(node.Left!, context, depth);
            return left.AsBool() ? new StencilValue(true) : new StencilValue(EvaluateNode(node.Right!, context, depth).AsBool());
        }

        var lhs = EvaluateNode(node.Left!, context, depth);
        var rhs = EvaluateNode(node.Right!, context, depth);

        return node.Operator switch
        {
            StencilExpressionOperator.Add => Add(lhs, rhs),
            StencilExpressionOperator.Subtract => new StencilValue(lhs.AsDouble() - rhs.AsDouble()),
            StencilExpressionOperator.Multiply => new StencilValue(lhs.AsDouble() * rhs.AsDouble()),
            StencilExpressionOperator.Divide => new StencilValue(Math.Abs(rhs.AsDouble()) <= double.Epsilon ? 0 : lhs.AsDouble() / rhs.AsDouble()),
            StencilExpressionOperator.Equal => new StencilValue(Compare(lhs, rhs) == 0),
            StencilExpressionOperator.NotEqual => new StencilValue(Compare(lhs, rhs) != 0),
            StencilExpressionOperator.Greater => new StencilValue(Compare(lhs, rhs) > 0),
            StencilExpressionOperator.GreaterOrEqual => new StencilValue(Compare(lhs, rhs) >= 0),
            StencilExpressionOperator.Less => new StencilValue(Compare(lhs, rhs) < 0),
            StencilExpressionOperator.LessOrEqual => new StencilValue(Compare(lhs, rhs) <= 0),
            _ => StencilValue.Null
        };
    }

    private static StencilValue EvaluateCoalesce(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        var left = EvaluateNode(node.Left!, context, depth);
        return left.IsNull ? EvaluateNode(node.Right!, context, depth) : left;
    }

    private static StencilValue EvaluateMap(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        var key = EvaluateNode(node.Source!, context, depth).AsString();
        if (node.MapEntries.TryGetValue(key, out var mapped))
            return EvaluateNode(mapped, context, depth);

        return node.Default is null ? StencilValue.Null : EvaluateNode(node.Default, context, depth);
    }

    private static StencilValue EvaluateToken(StencilExpressionNode node, StencilEvalContext context, int depth)
    {
        var fallback = node.Default is null ? string.Empty : EvaluateNode(node.Default, context, depth).AsString();
        return new StencilValue(context.Tokens?.Resolve(node.Name ?? string.Empty, fallback) ?? fallback);
    }

    private static StencilValue Add(StencilValue left, StencilValue right)
    {
        var leftIsNumber = left.IsNumeric(out var leftNumber);
        var rightIsNumber = right.IsNumeric(out var rightNumber);
        if (leftIsNumber && rightIsNumber)
            return new StencilValue(leftNumber + rightNumber);

        return new StencilValue(left.AsString() + right.AsString());
    }

    private static int Compare(StencilValue left, StencilValue right)
    {
        var leftIsNumber = left.IsNumeric(out var leftNumber);
        var rightIsNumber = right.IsNumeric(out var rightNumber);
        if (leftIsNumber && rightIsNumber)
            return leftNumber.CompareTo(rightNumber);

        return string.Compare(left.AsString(), right.AsString(), StringComparison.Ordinal);
    }
}
