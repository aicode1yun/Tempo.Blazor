#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Base type for expression AST nodes.</summary>
public abstract record ExpressionNode;

/// <summary>Literal expression node.</summary>
public sealed record LiteralExpressionNode(ExpressionValue Value) : ExpressionNode;

/// <summary>Member access expression node.</summary>
public sealed record MemberAccessExpressionNode(IReadOnlyList<string> Path, ExpressionNode? Target = null) : ExpressionNode;

/// <summary>Unary operator expression node.</summary>
public sealed record UnaryExpressionNode(ExpressionUnaryOperator Operator, ExpressionNode Operand) : ExpressionNode;

/// <summary>Binary operator expression node.</summary>
public sealed record BinaryExpressionNode(
    ExpressionBinaryOperator Operator,
    ExpressionNode Left,
    ExpressionNode Right) : ExpressionNode;

/// <summary>Function call expression node.</summary>
public sealed record FunctionCallExpressionNode(
    string Name,
    IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;

/// <summary>Aggregate expression node parsed for later processing phases.</summary>
public sealed record AggregateExpressionNode(
    ReportAggregateFunction Aggregate,
    ExpressionNode? ValueExpression,
    ReportAggregateScope Scope) : ExpressionNode;

/// <summary>Unary operators.</summary>
public enum ExpressionUnaryOperator
{
    /// <summary>Numeric negation.</summary>
    Negate,

    /// <summary>Logical negation.</summary>
    Not,
}

/// <summary>Binary operators.</summary>
public enum ExpressionBinaryOperator
{
    /// <summary>Addition or string concatenation.</summary>
    Add,

    /// <summary>Subtraction.</summary>
    Subtract,

    /// <summary>Multiplication.</summary>
    Multiply,

    /// <summary>Division.</summary>
    Divide,

    /// <summary>Modulo.</summary>
    Modulo,

    /// <summary>Equality comparison.</summary>
    Equal,

    /// <summary>Inequality comparison.</summary>
    NotEqual,

    /// <summary>Less-than comparison.</summary>
    LessThan,

    /// <summary>Less-than-or-equal comparison.</summary>
    LessThanOrEqual,

    /// <summary>Greater-than comparison.</summary>
    GreaterThan,

    /// <summary>Greater-than-or-equal comparison.</summary>
    GreaterThanOrEqual,

    /// <summary>Logical and.</summary>
    And,

    /// <summary>Logical or.</summary>
    Or,
}

/// <summary>Aggregate functions parsed as AST nodes.</summary>
public enum ReportAggregateFunction
{
    /// <summary>Sum aggregate.</summary>
    Sum,

    /// <summary>Count aggregate.</summary>
    Count,

    /// <summary>Distinct count aggregate.</summary>
    CountDistinct,

    /// <summary>Minimum aggregate.</summary>
    Min,

    /// <summary>Maximum aggregate.</summary>
    Max,

    /// <summary>Average aggregate.</summary>
    Avg,

    /// <summary>First value aggregate.</summary>
    First,

    /// <summary>Last value aggregate.</summary>
    Last,
}

/// <summary>Aggregate evaluation scope.</summary>
public enum ReportAggregateScope
{
    /// <summary>Current group scope.</summary>
    Group,

    /// <summary>Current page scope.</summary>
    Page,

    /// <summary>Whole report scope.</summary>
    Report,
}

#pragma warning restore MA0016, MA0048
