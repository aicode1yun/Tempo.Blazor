using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Tests.Expressions;

public sealed class ExpressionSafetyTests
{
    [Fact]
    public void Parse_RejectsExpressionsLongerThanConfiguredLimit()
    {
        var expression = "=" + new string('1', 32);

        var act = () => ExpressionParser.Parse(
            expression,
            new ExpressionParseOptions { MaxExpressionLength = 16 });

        act.Should().Throw<ExpressionParseException>()
            .Which.Diagnostic.Code.Should().Be("ExpressionParser.ExpressionTooLong");
    }

    [Fact]
    public void Parse_RejectsExpressionsDeeperThanConfiguredLimit()
    {
        var act = () => ExpressionParser.Parse(
            "=((((Fields.Price))))",
            new ExpressionParseOptions { MaxDepth = 2 });

        act.Should().Throw<ExpressionParseException>()
            .Which.Diagnostic.Code.Should().Be("ExpressionParser.ExpressionTooDeep");
    }
}
