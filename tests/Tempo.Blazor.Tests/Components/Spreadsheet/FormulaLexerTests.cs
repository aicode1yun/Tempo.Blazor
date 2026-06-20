using Tempo.Blazor.Components.Spreadsheet.Formula;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaLexerTests
{
    [Fact]
    public void Tokenize_Number()
    {
        var tokens = FormulaLexer.Tokenize("=42");
        tokens.Should().Contain(t => t.Type == TokenType.Number && t.Value == "42");
    }

    [Fact]
    public void Tokenize_DecimalNumber()
    {
        var tokens = FormulaLexer.Tokenize("=3.14");
        tokens.Should().Contain(t => t.Type == TokenType.Number && t.Value == "3.14");
    }

    [Fact]
    public void Tokenize_String()
    {
        var tokens = FormulaLexer.Tokenize("=\"hello\"");
        tokens.Should().Contain(t => t.Type == TokenType.String && t.Value == "hello");
    }

    [Fact]
    public void Tokenize_BooleanTrue()
    {
        var tokens = FormulaLexer.Tokenize("=TRUE");
        tokens.Should().Contain(t => t.Type == TokenType.Boolean && t.Value == "TRUE");
    }

    [Fact]
    public void Tokenize_BooleanFalse()
    {
        var tokens = FormulaLexer.Tokenize("=FALSE");
        tokens.Should().Contain(t => t.Type == TokenType.Boolean && t.Value == "FALSE");
    }

    [Fact]
    public void Tokenize_CellRef()
    {
        var tokens = FormulaLexer.Tokenize("=A1");
        tokens.Should().Contain(t => t.Type == TokenType.CellRef && t.Value == "A1");
    }

    [Fact]
    public void Tokenize_AbsoluteCellRef()
    {
        var tokens = FormulaLexer.Tokenize("=$B$2");
        tokens.Should().Contain(t => t.Type == TokenType.CellRef && t.Value == "$B$2");
    }

    [Fact]
    public void Tokenize_AbsoluteColRelativeRow()
    {
        var tokens = FormulaLexer.Tokenize("=$A1");
        tokens.Should().Contain(t => t.Type == TokenType.CellRef && t.Value == "$A1");
    }

    [Fact]
    public void Tokenize_RelativeColAbsoluteRow()
    {
        var tokens = FormulaLexer.Tokenize("=A$1");
        tokens.Should().Contain(t => t.Type == TokenType.CellRef && t.Value == "A$1");
    }

    [Fact]
    public void Tokenize_AbsoluteRangeRef()
    {
        var tokens = FormulaLexer.Tokenize("=$A$1:$B$3");
        tokens.Should().Contain(t => t.Type == TokenType.RangeRef && t.Value == "$A$1:$B$3");
    }

    [Fact]
    public void Tokenize_RangeRef()
    {
        var tokens = FormulaLexer.Tokenize("=A1:B10");
        tokens.Should().Contain(t => t.Type == TokenType.RangeRef && t.Value == "A1:B10");
    }

    [Fact]
    public void Tokenize_FunctionCall()
    {
        var tokens = FormulaLexer.Tokenize("=SUM(A1:A10)");
        tokens[0].Type.Should().Be(TokenType.Equal);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "SUM");
        tokens.Should().Contain(t => t.Type == TokenType.RangeRef && t.Value == "A1:A10");
    }

    [Fact]
    public void Tokenize_Operators()
    {
        var tokens = FormulaLexer.Tokenize("=A1+B1-C1*D1/E1^F1");
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Minus);
        tokens.Should().Contain(t => t.Type == TokenType.Multiply);
        tokens.Should().Contain(t => t.Type == TokenType.Divide);
        tokens.Should().Contain(t => t.Type == TokenType.Power);
    }

    [Fact]
    public void Tokenize_ComparisonOperators()
    {
        var tokens = FormulaLexer.Tokenize("=A1=B1<>C1<D1>E1<=E1>=F1");
        tokens.Should().Contain(t => t.Type == TokenType.Equal);
        tokens.Should().Contain(t => t.Type == TokenType.NotEqual);
        tokens.Should().Contain(t => t.Type == TokenType.LessThan);
        tokens.Should().Contain(t => t.Type == TokenType.GreaterThan);
        tokens.Should().Contain(t => t.Type == TokenType.LessThanOrEqual);
        tokens.Should().Contain(t => t.Type == TokenType.GreaterThanOrEqual);
    }

    [Fact]
    public void Tokenize_Percent()
    {
        var tokens = FormulaLexer.Tokenize("=50%");
        tokens.Should().Contain(t => t.Type == TokenType.Percent);
    }

    [Fact]
    public void Tokenize_EndsWithEndToken()
    {
        var tokens = FormulaLexer.Tokenize("=1");
        tokens.Last().Type.Should().Be(TokenType.End);
    }
}
