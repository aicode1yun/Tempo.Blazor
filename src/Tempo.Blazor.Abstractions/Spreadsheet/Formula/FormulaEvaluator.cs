using System.Globalization;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Evaluates a <see cref="FormulaNode"/> AST against a <see cref="FormulaContext"/>.
/// </summary>
public sealed class FormulaEvaluator
{
    private readonly FunctionRegistry _registry;

    public FormulaEvaluator(FunctionRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Evaluates the given AST node.</summary>
    public object? Evaluate(FormulaNode node, FormulaContext context)
    {
        return node switch
        {
            NumberNode n => n.Value,
            StringNode s => s.Value,
            BooleanNode b => b.Value,
            CellRefNode c => context.ResolveCellRef(c.Ref),
            RangeRefNode r => context.ResolveRangeRef(r.StartRef, r.EndRef),
            UnaryOpNode u => EvaluateUnary(u, context),
            BinaryOpNode b => EvaluateBinary(b, context),
            FunctionCallNode f => EvaluateFunction(f, context),
            _ => new FormulaError("#VALUE!")
        };
    }

    private object? EvaluateUnary(UnaryOpNode node, FormulaContext context)
    {
        var operand = Evaluate(node.Operand, context);
        if (operand is FormulaError err) return err;
        var num = ToDouble(operand);

        return node.Operator switch
        {
            "+" => num,
            "-" => -num,
            "%" => num / 100.0,
            _ => new FormulaError("#VALUE!")
        };
    }

    private object? EvaluateBinary(BinaryOpNode node, FormulaContext context)
    {
        var left = Evaluate(node.Left, context);
        var right = Evaluate(node.Right, context);

        if (left is FormulaError le) return le;
        if (right is FormulaError re) return re;

        // String concatenation with &
        if (node.Operator == "&")
        {
            return $"{left}{right}";
        }

        var leftNum = ToDouble(left);
        var rightNum = ToDouble(right);

        return node.Operator switch
        {
            "+" => leftNum + rightNum,
            "-" => leftNum - rightNum,
            "*" => leftNum * rightNum,
            "/" => rightNum == 0 ? new FormulaError("#DIV/0!") : leftNum / rightNum,
            "^" => Math.Pow(leftNum, rightNum),
            "=" => leftNum == rightNum,
            "<>" => leftNum != rightNum,
            "<" => leftNum < rightNum,
            ">" => leftNum > rightNum,
            "<=" => leftNum <= rightNum,
            ">=" => leftNum >= rightNum,
            _ => new FormulaError("#VALUE!")
        };
    }

    private static readonly HashSet<string> _refFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROW", "COLUMN", "ROWS", "COLUMNS", "INDEX", "OFFSET", "INDIRECT", "ADDRESS", "AREAS", "VLOOKUP", "HLOOKUP", "MATCH"
    };

    private object? EvaluateFunction(FunctionCallNode node, FormulaContext context)
    {
        var args = new List<object?>();
        foreach (var arg in node.Arguments)
        {
            try
            {
                if (_refFunctions.Contains(node.Name) && arg is CellRefNode cellNode)
                {
                    args.Add(cellNode.Ref);
                }
                else if (_refFunctions.Contains(node.Name) && arg is RangeRefNode rangeNode)
                {
                    args.Add($"{rangeNode.StartRef}:{rangeNode.EndRef}");
                }
                else if (arg is RangeRefNode rangeNode2)
                {
                    args.Add(context.ResolveRangeRef(rangeNode2.StartRef, rangeNode2.EndRef));
                }
                else if (arg is CellRefNode cellNode2)
                {
                    args.Add(context.ResolveCellRef(cellNode2.Ref));
                }
                else
                {
                    args.Add(Evaluate(arg, context));
                }
            }
            catch
            {
                args.Add(new FormulaError("#VALUE!"));
            }
        }

        try
        {
            return _registry.Invoke(node.Name, context, args);
        }
        catch
        {
            return new FormulaError("#NAME?");
        }
    }

    private static double ToDouble(object? value)
    {
        if (value is null) return 0;
        if (value is FormulaError) return 0;
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal dec) return (double)dec;
        if (value is float f) return f;
        if (value is bool b) return b ? 1 : 0;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (value is List<object?> list)
        {
            return list.Select(ToDouble).Sum();
        }
        return 0;
    }
}
