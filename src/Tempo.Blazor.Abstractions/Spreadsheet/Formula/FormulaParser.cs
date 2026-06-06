using System.Globalization;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

public sealed class FormulaParser
{
    private readonly List<FormulaToken> _tokens;
    private int _pos;

    public FormulaParser(List<FormulaToken> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    private FormulaToken Current => _tokens[_pos];

    private FormulaToken Advance()
    {
        var token = Current;
        if (Current.Type != TokenType.End)
            _pos++;
        return token;
    }

    private bool Match(params TokenType[] types)
    {
        if (types.Contains(Current.Type))
        {
            Advance();
            return true;
        }
        return false;
    }

    private void Expect(TokenType type, string message)
    {
        if (Current.Type != type)
            throw new InvalidOperationException($"Expected {type} but found {Current.Type} ({Current.Value}). {message}");
        Advance();
    }

    public FormulaNode Parse()
    {
        if (Current.Type == TokenType.Equal)
            Advance(); // skip leading '='
        return Expression();
    }

    private FormulaNode Expression() => Comparison();

    private FormulaNode Comparison()
    {
        var node = Additive();
        while (Current.Type is TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or TokenType.GreaterThan or TokenType.LessThanOrEqual or TokenType.GreaterThanOrEqual)
        {
            var op = Advance().Value;
            var right = Additive();
            node = new BinaryOpNode(op, node, right);
        }
        return node;
    }

    private FormulaNode Additive()
    {
        var node = Multiplicative();
        while (Current.Type is TokenType.Plus or TokenType.Minus or TokenType.Ampersand)
        {
            var op = Advance().Value;
            var right = Multiplicative();
            node = new BinaryOpNode(op, node, right);
        }
        return node;
    }

    private FormulaNode Multiplicative()
    {
        var node = Power();
        while (Current.Type is TokenType.Multiply or TokenType.Divide)
        {
            var op = Advance().Value;
            var right = Power();
            node = new BinaryOpNode(op, node, right);
        }
        return node;
    }

    private FormulaNode Power()
    {
        var node = Unary();
        if (Match(TokenType.Power))
        {
            var right = Power(); // right associative
            return new BinaryOpNode("^", node, right);
        }
        return node;
    }

    private FormulaNode Unary()
    {
        if (Match(TokenType.Plus))
            return new UnaryOpNode("+", Unary());
        if (Match(TokenType.Minus))
            return new UnaryOpNode("-", Unary());
        return Postfix();
    }

    private FormulaNode Postfix()
    {
        var node = Primary();
        if (Match(TokenType.Percent))
        {
            return new UnaryOpNode("%", node);
        }
        return node;
    }

    private FormulaNode Primary()
    {
        switch (Current.Type)
        {
            case TokenType.Number:
                {
                    var val = double.Parse(Advance().Value, CultureInfo.InvariantCulture);
                    return new NumberNode(val);
                }
            case TokenType.String:
                return new StringNode(Advance().Value);
            case TokenType.Boolean:
                return new BooleanNode(Advance().Value.Equals("TRUE", StringComparison.OrdinalIgnoreCase));
            case TokenType.CellRef:
                return new CellRefNode(Advance().Value);
            case TokenType.RangeRef:
                {
                    var range = Advance().Value;
                    var parts = range.Split(':');
                    return new RangeRefNode(parts[0], parts[1]);
                }
            case TokenType.Identifier:
                return ParseIdentifierOrFunctionCall();
            case TokenType.LParen:
                {
                    Advance();
                    var expr = Expression();
                    Expect(TokenType.RParen, "Expected ')' after expression.");
                    return expr;
                }
            default:
                throw new InvalidOperationException($"Unexpected token {Current.Type} ({Current.Value}).");
        }
    }

    private FormulaNode ParseIdentifierOrFunctionCall()
    {
        var name = Advance().Value;

        // Named range reference (no opening parenthesis)
        if (Current.Type != TokenType.LParen)
        {
            return new NamedRangeRefNode(name);
        }

        Expect(TokenType.LParen, $"Expected '(' after function name {name}.");

        var args = new List<FormulaNode>();
        if (Current.Type != TokenType.RParen)
        {
            args.Add(Expression());
            while (Match(TokenType.Comma, TokenType.Semicolon))
            {
                args.Add(Expression());
            }
        }
        Expect(TokenType.RParen, $"Expected ')' after arguments of {name}.");
        return new FunctionCallNode(name.ToUpperInvariant(), args);
    }
}
