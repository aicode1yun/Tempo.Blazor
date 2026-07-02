using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilEvaluatorTests
{
    [Fact]
    public void Evaluate_Property_ReturnsPropValue()
    {
        Evaluate("{label}").AsString().Should().Be("Save");
    }

    [Fact]
    public void Evaluate_Literal_ReturnsLiteralValue()
    {
        Evaluate("\"Save\"").AsString().Should().Be("Save");
    }

    [Fact]
    public void Evaluate_NullCoalescing_UsesFallbackForNull()
    {
        Evaluate("{missing ?? \"OK\"}").AsString().Should().Be("OK");
    }

    [Fact]
    public void Evaluate_Ternary_UsesBooleanCondition()
    {
        Evaluate("{isPrimary ? \"primary\" : \"secondary\"}").AsString().Should().Be("primary");
    }

    [Fact]
    public void Evaluate_Comparison_ReturnsBoolean()
    {
        Evaluate("{count >= 3}").AsBool().Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalOperators_ReturnBoolean()
    {
        Evaluate("{!disabled && visible || loading}").AsBool().Should().BeTrue();
    }

    [Fact]
    public void Evaluate_PlusConcat_ConcatenatesWhenEitherOperandIsString()
    {
        Evaluate("{\"Hello \" + label}").AsString().Should().Be("Hello Save");
    }

    [Fact]
    public void Evaluate_PlusArithmetic_AddsNumbers()
    {
        Evaluate("{size.w + 12}").AsDouble().Should().Be(212);
    }

    [Fact]
    public void Evaluate_SizeHeight_ReturnsContextHeight()
    {
        Evaluate("{size.h}").AsDouble().Should().Be(48);
    }

    [Fact]
    public void Evaluate_RepeatIndex_ReturnsContextRepeatIndex()
    {
        Evaluate("{repeat.index}", Context().WithRepeatIndex(4)).AsDouble().Should().Be(4);
    }

    [Fact]
    public void Evaluate_Map_ReturnsMatchingEntry()
    {
        Evaluate("$map{variant: primary=filled, danger=danger, *=default}").AsString().Should().Be("filled");
    }

    [Fact]
    public void Evaluate_Map_UsesDefaultEntry()
    {
        var context = Context(new Dictionary<string, object?>
        {
            ["variant"] = "ghost"
        });

        Evaluate("$map{variant: primary=filled, danger=danger, *=default}", context).AsString().Should().Be("default");
    }

    [Fact]
    public void StencilValue_CoercesStringsNumbersBooleansAndNulls()
    {
        new StencilValue("12.5").AsDouble().Should().Be(12.5);
        new StencilValue(0).AsBool().Should().BeFalse();
        new StencilValue("true").AsBool().Should().BeTrue();
        StencilValue.Null.IsNull.Should().BeTrue();
        StencilValue.Null.AsString().Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("{label")]
    [InlineData("{unknown(\"x\")}")]
    [InlineData("$map{variant primary=filled}")]
    [InlineData("{System.Environment.Exit(0)}")]
    [InlineData("{1.2.3}")]
    public void Evaluate_MalformedOrInjectionInput_NeverThrowsAndReturnsRawFallback(string input)
    {
        var evaluator = new StencilEvaluator();
        var act = () => evaluator.Evaluate(input, Context());

        act.Should().NotThrow();
        evaluator.Evaluate(input, Context()).AsString().Should().Be(input);
    }

    [Theory]
    [MemberData(nameof(DeepMalformedInputs))]
    public void Evaluate_DeepOrHugeInput_NeverThrowsAndReturnsRawFallback(string input)
    {
        var evaluator = new StencilEvaluator();
        var act = () => evaluator.Evaluate(input, Context());

        act.Should().NotThrow();
        evaluator.Evaluate(input, Context()).AsString().Should().Be(input);
    }

    private static StencilValue Evaluate(string expression, StencilEvalContext? context = null)
    {
        return new StencilEvaluator().Evaluate(StencilExpression.Parse(expression), context ?? Context());
    }

    private static StencilEvalContext Context(IReadOnlyDictionary<string, object?>? props = null)
    {
        return new StencilEvalContext(
            Props: props ?? new Dictionary<string, object?>
            {
                ["label"] = "Save",
                ["count"] = 3,
                ["isPrimary"] = true,
                ["disabled"] = false,
                ["visible"] = true,
                ["loading"] = false,
                ["variant"] = "primary"
            },
            SizeW: 200,
            SizeH: 48,
            RepeatIndex: 2,
            Tokens: null);
    }

    public static IEnumerable<object[]> DeepMalformedInputs()
        => StencilExpressionTests.DeepMalformedInputs();
}
