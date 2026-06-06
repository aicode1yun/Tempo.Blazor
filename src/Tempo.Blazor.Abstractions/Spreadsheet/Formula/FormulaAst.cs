namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>Base class for all nodes in the formula AST.</summary>
public abstract class FormulaNode;

/// <summary>A numeric literal.</summary>
public sealed class NumberNode : FormulaNode
{
    public double Value { get; }
    public NumberNode(double value) => Value = value;
}

/// <summary>A string literal.</summary>
public sealed class StringNode : FormulaNode
{
    public string Value { get; }
    public StringNode(string value) => Value = value;
}

/// <summary>A boolean literal.</summary>
public sealed class BooleanNode : FormulaNode
{
    public bool Value { get; }
    public BooleanNode(bool value) => Value = value;
}

/// <summary>A cell reference like A1 or $B$2.</summary>
public sealed class CellRefNode : FormulaNode
{
    public string Ref { get; }
    public CellRefNode(string reference) => Ref = reference;
}

/// <summary>A range reference like A1:B10.</summary>
public sealed class RangeRefNode : FormulaNode
{
    public string StartRef { get; }
    public string EndRef { get; }
    public RangeRefNode(string startRef, string endRef)
    {
        StartRef = startRef;
        EndRef = endRef;
    }
}

/// <summary>A unary operation (e.g. -A1, +5).</summary>
public sealed class UnaryOpNode : FormulaNode
{
    public string Operator { get; }
    public FormulaNode Operand { get; }
    public UnaryOpNode(string op, FormulaNode operand)
    {
        Operator = op;
        Operand = operand;
    }
}

/// <summary>A binary operation (e.g. A1 + B1).</summary>
public sealed class BinaryOpNode : FormulaNode
{
    public string Operator { get; }
    public FormulaNode Left { get; }
    public FormulaNode Right { get; }
    public BinaryOpNode(string op, FormulaNode left, FormulaNode right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }
}

/// <summary>A named range reference like Sales or TaxRate.</summary>
public sealed class NamedRangeRefNode : FormulaNode
{
    public string Name { get; }
    public NamedRangeRefNode(string name) => Name = name;
}

/// <summary>A function call like SUM(A1:A10).</summary>
public sealed class FunctionCallNode : FormulaNode
{
    public string Name { get; }
    public List<FormulaNode> Arguments { get; }
    public FunctionCallNode(string name, List<FormulaNode> arguments)
    {
        Name = name;
        Arguments = arguments;
    }
}
