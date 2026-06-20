using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Models;

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
            NamedRangeRefNode n => ResolveNamedRange(n, context),
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

    private static readonly HashSet<string> _errorHandlingFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "IFERROR", "ISERROR", "ISERR", "ISBLANK", "ISNUMBER", "ISTEXT", "ISLOGICAL", "ISEVEN", "ISODD"
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

        if (!_errorHandlingFunctions.Contains(node.Name))
        {
            var firstError = args.OfType<FormulaError>().FirstOrDefault();
            if (firstError is not null)
                return firstError;
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

    private static object? ResolveNamedRange(NamedRangeRefNode node, FormulaContext context)
    {
        var refersTo = context.ResolveNamedRange(node.Name);
        if (string.IsNullOrWhiteSpace(refersTo))
            return new FormulaError("#NAME?");

        // Strip leading '=' if present (named ranges may store formulas)
        var target = refersTo.StartsWith('=') ? refersTo[1..] : refersTo;

        // Split off an optional sheet qualifier (Sheet1!A1:A3 or 'My Sheet'!A1).
        var (sheetName, body) = ParseSheetQualifiedRef(target);
        var sheet = context.Sheet;
        if (sheetName is not null)
        {
            var qualified = context.Workbook?.Sheets
                .FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));
            if (qualified is null)
                return new FormulaError("#NAME?");
            sheet = qualified;
        }

        // Try to parse the body as a single cell or range (covers both A1 and A1:B10).
        try
        {
            var range = SpreadsheetRange.Parse(body);

            // Single cell → return its raw value (preserves text/bool, not just numbers).
            if (range.RowCount == 1 && range.ColumnCount == 1)
            {
                var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(range.StartCol)}{range.StartRow + 1}";
                return sheet.Cells.TryGetValue(cellRef, out var cell) ? cell.Value : 0d;
            }

            // Multi-cell range → sum the numeric values on the resolved sheet.
            double sum = 0;
            foreach (var cellRef in range.CellRefs)
                if (sheet.Cells.TryGetValue(cellRef, out var cell))
                    sum += ToDouble(cell.Value);
            return sum;
        }
        catch { /* not an A1 reference */ }

        // Try to parse as a numeric constant.
        if (double.TryParse(body, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var constant))
            return constant;

        return new FormulaError("#NAME?");
    }

    private static (string? SheetName, string CellRef) ParseSheetQualifiedRef(string target)
    {
        var exclamationIndex = target.IndexOf('!');
        if (exclamationIndex < 0)
            return (null, target);

        var sheetName = target[..exclamationIndex].Trim();
        if (sheetName.StartsWith('\'') && sheetName.EndsWith('\''))
            sheetName = sheetName[1..^1];

        return (sheetName, target[(exclamationIndex + 1)..]);
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
