#pragma warning disable MA0048

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Options controlling expression parsing.</summary>
public sealed record ExpressionParseOptions
{
    /// <summary>Whether aggregate functions are accepted by the parser.</summary>
    public bool AllowAggregates { get; init; } = true;

    /// <summary>Maximum source expression length.</summary>
    public int MaxExpressionLength { get; init; } = 4096;

    /// <summary>Maximum parenthesized expression depth.</summary>
    public int MaxDepth { get; init; } = 64;
}

/// <summary>Options controlling expression evaluation.</summary>
public sealed record ExpressionEvaluationOptions
{
    /// <summary>Maximum number of AST node evaluations before aborting.</summary>
    public int MaxEvaluationSteps { get; init; } = 10_000;

    /// <summary>Maximum wall-clock evaluation time.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(1);
}

#pragma warning restore MA0048
