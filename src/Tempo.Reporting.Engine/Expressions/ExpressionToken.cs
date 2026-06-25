#pragma warning disable MA0048

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Expression token kind.</summary>
public enum ExpressionTokenKind
{
    /// <summary>End of input marker.</summary>
    EndOfInput,

    /// <summary>Numeric literal.</summary>
    Number,

    /// <summary>String literal.</summary>
    String,

    /// <summary>Identifier.</summary>
    Identifier,

    /// <summary>Dot token.</summary>
    Dot,

    /// <summary>Comma token.</summary>
    Comma,

    /// <summary>Opening parenthesis.</summary>
    OpenParen,

    /// <summary>Closing parenthesis.</summary>
    CloseParen,

    /// <summary>Plus operator.</summary>
    Plus,

    /// <summary>Minus operator.</summary>
    Minus,

    /// <summary>Multiplication operator.</summary>
    Star,

    /// <summary>Division operator.</summary>
    Slash,

    /// <summary>Modulo operator.</summary>
    Percent,

    /// <summary>Equality operator.</summary>
    Equal,

    /// <summary>Inequality operator.</summary>
    NotEqual,

    /// <summary>Less-than operator.</summary>
    Less,

    /// <summary>Less-than-or-equal operator.</summary>
    LessOrEqual,

    /// <summary>Greater-than operator.</summary>
    Greater,

    /// <summary>Greater-than-or-equal operator.</summary>
    GreaterOrEqual,
}

/// <summary>Expression token with source position and optional literal value.</summary>
public sealed record ExpressionToken(
    ExpressionTokenKind Kind,
    string Text,
    object? Value,
    int Line,
    int Column);

#pragma warning restore MA0048
