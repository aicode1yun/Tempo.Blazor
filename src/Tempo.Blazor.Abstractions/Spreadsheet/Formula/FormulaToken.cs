namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Represents a single token produced by the <see cref="FormulaLexer"/>.
/// </summary>
public sealed class FormulaToken
{
    public FormulaToken(TokenType type, string value)
    {
        Type = type;
        Value = value;
    }

    public TokenType Type { get; }
    public string Value { get; }

    public override string ToString() => $"{Type}:{Value}";
}

public enum TokenType
{
    End,
    Number,
    String,
    Boolean,
    Identifier,
    CellRef,
    RangeRef,
    NamedRange,

    Plus,
    Minus,
    Multiply,
    Divide,
    Power,
    Percent,

    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,

    Ampersand,
    Comma,
    Colon,
    Semicolon,
    LParen,
    RParen,
}
