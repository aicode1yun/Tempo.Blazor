using Tempo.Blazor.Components.Spreadsheet.Formula;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaParserTests
{
    private static FormulaNode Parse(string formula)
    {
        var tokens = FormulaLexer.Tokenize(formula);
        var parser = new FormulaParser(tokens);
        return parser.Parse();
    }

    [Fact]
    public void Parse_NumberLiteral()
    {
        var node = Parse("=42");
        node.Should().BeOfType<NumberNode>().Which.Value.Should().Be(42);
    }

    [Fact]
    public void Parse_StringLiteral()
    {
        var node = Parse("=\"hello\"");
        node.Should().BeOfType<StringNode>().Which.Value.Should().Be("hello");
    }

    [Fact]
    public void Parse_BooleanLiteral()
    {
        var node = Parse("=TRUE");
        node.Should().BeOfType<BooleanNode>().Which.Value.Should().BeTrue();
    }

    [Fact]
    public void Parse_CellRef()
    {
        var node = Parse("=A1");
        node.Should().BeOfType<CellRefNode>().Which.Ref.Should().Be("A1");
    }

    [Fact]
    public void Parse_RangeRef()
    {
        var node = Parse("=A1:B10");
        var range = node.Should().BeOfType<RangeRefNode>().Subject;
        range.StartRef.Should().Be("A1");
        range.EndRef.Should().Be("B10");
    }

    [Fact]
    public void Parse_UnaryMinus()
    {
        var node = Parse("=-5");
        var unary = node.Should().BeOfType<UnaryOpNode>().Subject;
        unary.Operator.Should().Be("-");
        unary.Operand.Should().BeOfType<NumberNode>().Which.Value.Should().Be(5);
    }

    [Fact]
    public void Parse_BinaryAdd()
    {
        var node = Parse("=1+2");
        var binary = node.Should().BeOfType<BinaryOpNode>().Subject;
        binary.Operator.Should().Be("+");
        binary.Left.Should().BeOfType<NumberNode>().Which.Value.Should().Be(1);
        binary.Right.Should().BeOfType<NumberNode>().Which.Value.Should().Be(2);
    }

    [Fact]
    public void Parse_BinaryMultiply()
    {
        var node = Parse("=2*3");
        var binary = node.Should().BeOfType<BinaryOpNode>().Subject;
        binary.Operator.Should().Be("*");
    }

    [Fact]
    public void Parse_Precedence_MultiplyBeforeAdd()
    {
        var node = Parse("=1+2*3");
        var binary = node.Should().BeOfType<BinaryOpNode>().Subject;
        binary.Operator.Should().Be("+");
        binary.Left.Should().BeOfType<NumberNode>().Which.Value.Should().Be(1);
        binary.Right.Should().BeOfType<BinaryOpNode>().Which.Operator.Should().Be("*");
    }

    [Fact]
    public void Parse_Parentheses()
    {
        var node = Parse("=(1+2)*3");
        var binary = node.Should().BeOfType<BinaryOpNode>().Subject;
        binary.Operator.Should().Be("*");
        binary.Left.Should().BeOfType<BinaryOpNode>().Which.Operator.Should().Be("+");
    }

    [Fact]
    public void Parse_FunctionCall()
    {
        var node = Parse("=SUM(A1:A10)");
        var fn = node.Should().BeOfType<FunctionCallNode>().Subject;
        fn.Name.Should().Be("SUM");
        fn.Arguments.Should().HaveCount(1);
        fn.Arguments[0].Should().BeOfType<RangeRefNode>();
    }

    [Fact]
    public void Parse_FunctionCall_MultipleArgs()
    {
        var node = Parse("=SUM(A1,A2,A3)");
        var fn = node.Should().BeOfType<FunctionCallNode>().Subject;
        fn.Name.Should().Be("SUM");
        fn.Arguments.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_Comparison()
    {
        var node = Parse("=A1>B1");
        var binary = node.Should().BeOfType<BinaryOpNode>().Subject;
        binary.Operator.Should().Be(">");
    }

    [Fact]
    public void Parse_Percent()
    {
        var node = Parse("=50%");
        var unary = node.Should().BeOfType<UnaryOpNode>().Subject;
        unary.Operator.Should().Be("%");
    }
}
