using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilExpressionTests
{
    [Fact]
    public void Parse_PropReference_RecognizesPropertyNode()
    {
        var expression = StencilExpression.Parse("{label}");

        expression.IsMalformed.Should().BeFalse();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Property);
        expression.Root.Name.Should().Be("label");
    }

    [Fact]
    public void Parse_Literal_RecognizesLiteralNode()
    {
        var expression = StencilExpression.Parse("\"Save\"");

        expression.IsMalformed.Should().BeFalse();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Literal);
        expression.Root.Value.Should().Be("Save");
    }

    [Fact]
    public void Parse_NullCoalescing_RecognizesCoalesceNode()
    {
        var expression = StencilExpression.Parse("{label ?? \"OK\"}");

        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Coalesce);
        expression.Root.Left!.Kind.Should().Be(StencilExpressionNodeKind.Property);
        expression.Root.Right!.Value.Should().Be("OK");
    }

    [Fact]
    public void Parse_Ternary_RecognizesConditionalNode()
    {
        var expression = StencilExpression.Parse("{isPrimary ? \"primary\" : \"secondary\"}");

        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Conditional);
        expression.Root.Condition!.Name.Should().Be("isPrimary");
        expression.Root.WhenTrue!.Value.Should().Be("primary");
        expression.Root.WhenFalse!.Value.Should().Be("secondary");
    }

    [Fact]
    public void Parse_Comparison_RecognizesComparisonNode()
    {
        var expression = StencilExpression.Parse("{count >= 3}");

        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Binary);
        expression.Root.Operator.Should().Be(StencilExpressionOperator.GreaterOrEqual);
    }

    [Fact]
    public void Parse_LogicalOperators_RecognizesPrecedence()
    {
        var expression = StencilExpression.Parse("{!disabled && visible || loading}");

        expression.Root.Operator.Should().Be(StencilExpressionOperator.Or);
        expression.Root.Left!.Operator.Should().Be(StencilExpressionOperator.And);
        expression.Root.Left.Left!.Kind.Should().Be(StencilExpressionNodeKind.Unary);
    }

    [Fact]
    public void Parse_PlusConcat_RecognizesAddNode()
    {
        var expression = StencilExpression.Parse("{\"Hello \" + label}");

        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Binary);
        expression.Root.Operator.Should().Be(StencilExpressionOperator.Add);
    }

    [Fact]
    public void Parse_PlusArithmetic_RecognizesAddNode()
    {
        var expression = StencilExpression.Parse("{size.w + 12}");

        expression.Root.Operator.Should().Be(StencilExpressionOperator.Add);
        expression.Root.Left!.Kind.Should().Be(StencilExpressionNodeKind.SizeWidth);
    }

    [Fact]
    public void Parse_Map_RecognizesMapNodeWithDefault()
    {
        var expression = StencilExpression.Parse("$map{variant: primary=filled, danger=danger, *=default}");

        expression.IsMalformed.Should().BeFalse();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Map);
        expression.Root.Source!.Name.Should().Be("variant");
        expression.Root.MapEntries.Should().ContainKey("primary");
        expression.Root.Default!.Value.Should().Be("default");
    }

    [Fact]
    public void Parse_TokenCall_RecognizesTokenNode()
    {
        var expression = StencilExpression.Parse("token(\"palette.primary\")");

        expression.IsMalformed.Should().BeFalse();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Token);
        expression.Root.Name.Should().Be("palette.primary");
    }

    [Fact]
    public void Parse_SizeWidth_RecognizesSizeWidthNode()
    {
        StencilExpression.Parse("{size.w}").Root.Kind.Should().Be(StencilExpressionNodeKind.SizeWidth);
    }

    [Fact]
    public void Parse_SizeHeight_RecognizesSizeHeightNode()
    {
        StencilExpression.Parse("{size.h}").Root.Kind.Should().Be(StencilExpressionNodeKind.SizeHeight);
    }

    [Fact]
    public void Parse_RepeatIndex_RecognizesRepeatIndexNode()
    {
        StencilExpression.Parse("{repeat.index}").Root.Kind.Should().Be(StencilExpressionNodeKind.RepeatIndex);
    }

    [Theory]
    [InlineData("{label")]
    [InlineData("{unknown(\"x\")}")]
    [InlineData("$map{variant primary=filled}")]
    [InlineData("{System.Environment.Exit(0)}")]
    [InlineData("{1.2.3}")]
    public void Parse_MalformedOrInjectionInput_NeverThrowsAndReturnsMalformedLiteral(string input)
    {
        var act = () => StencilExpression.Parse(input);

        act.Should().NotThrow();
        var expression = StencilExpression.Parse(input);
        expression.IsMalformed.Should().BeTrue();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Literal);
        expression.Root.Value.Should().Be(input);
    }

    [Theory]
    [MemberData(nameof(DeepMalformedInputs))]
    public void Parse_DeepOrHugeInput_NeverThrowsAndReturnsMalformedLiteral(string input)
    {
        var act = () => StencilExpression.Parse(input);

        act.Should().NotThrow();
        var expression = StencilExpression.Parse(input);
        expression.IsMalformed.Should().BeTrue();
        expression.Root.Kind.Should().Be(StencilExpressionNodeKind.Literal);
        expression.Root.Value.Should().Be(input);
    }

    public static IEnumerable<object[]> DeepMalformedInputs()
    {
        yield return ["{" + new string('(', 160) + "label" + new string(')', 160) + "}"];
        yield return ["{" + new string('!', 160) + "visible}"];
        yield return ["{" + string.Concat(Enumerable.Repeat("isPrimary ? ", 160)) + "\"yes\"" + string.Concat(Enumerable.Repeat(" : \"no\"", 160)) + "}"];
        yield return ["{" + string.Join("+", Enumerable.Repeat("1", 5000)) + "}"];
        yield return ["{" + new string('a', 40000) + "}"];
    }
}
