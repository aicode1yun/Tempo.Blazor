#pragma warning disable MA0048

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Exception thrown when an expression cannot be parsed.</summary>
public sealed class ExpressionParseException : Exception
{
    /// <summary>Creates a parse exception from a diagnostic.</summary>
    public ExpressionParseException(ExpressionDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Diagnostic describing the parse failure.</summary>
    public ExpressionDiagnostic Diagnostic { get; }
}

/// <summary>Exception thrown when an expression cannot be evaluated.</summary>
public sealed class ExpressionEvaluationException : Exception
{
    /// <summary>Creates an evaluation exception from a diagnostic.</summary>
    public ExpressionEvaluationException(ExpressionDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Diagnostic describing the evaluation failure.</summary>
    public ExpressionDiagnostic Diagnostic { get; }
}

#pragma warning restore MA0048
