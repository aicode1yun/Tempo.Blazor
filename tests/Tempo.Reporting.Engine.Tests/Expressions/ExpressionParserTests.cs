using System.Globalization;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Tests.Expressions;

public sealed class ExpressionParserTests
{
    [Fact]
    public void Parse_BuildsAstWithPrecedenceUnaryCallsAndMemberAccess()
    {
        var expression = ExpressionParser.Parse("=-Fields.Price + Round(Parameters.Rate * 2, 1)");

        var binary = expression.Should().BeOfType<BinaryExpressionNode>().Subject;
        binary.Operator.Should().Be(ExpressionBinaryOperator.Add);
        binary.Left.Should().BeOfType<UnaryExpressionNode>()
            .Which.Operand.Should().BeOfType<MemberAccessExpressionNode>()
            .Which.Path.Should().Equal("Fields", "Price");
        var call = binary.Right.Should().BeOfType<FunctionCallExpressionNode>().Subject;
        call.Name.Should().Be("Round");
        call.Arguments.Should().HaveCount(2);
        call.Arguments[0].Should().BeOfType<BinaryExpressionNode>()
            .Which.Operator.Should().Be(ExpressionBinaryOperator.Multiply);
    }

    [Fact]
    public void Parse_AggregateFunction_ProducesAggregateAstNodeWithScope()
    {
        var expression = ExpressionParser.Parse("=Sum(Fields.Total, \"group\")");

        var aggregate = expression.Should().BeOfType<AggregateExpressionNode>().Subject;
        aggregate.Aggregate.Should().Be(ReportAggregateFunction.Sum);
        aggregate.ValueExpression.Should().BeOfType<MemberAccessExpressionNode>()
            .Which.Path.Should().Equal("Fields", "Total");
        aggregate.Scope.Should().Be(ReportAggregateScope.Group);
    }

    [Fact]
    public void Parse_AggregateFunction_WhenAggregatesAreDisabled_ReturnsLocalizedError()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("cs-CZ");

            var act = () => ExpressionParser.Parse(
                "=Sum(Fields.Total)",
                new ExpressionParseOptions { AllowAggregates = false });

            var exception = act.Should().Throw<ExpressionParseException>().Which;
            exception.Diagnostic.Code.Should().Be("ExpressionParser.AggregateNotAllowed");
            exception.Message.Should().Contain("Agregační funkce");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Parse_MissingClosingParenthesis_ReportsRecoverablePosition()
    {
        var act = () => ExpressionParser.Parse("=IIf(Fields.Price > 0, \"ok\"");

        act.Should().Throw<ExpressionParseException>()
            .Which.Diagnostic.Should().Match<ExpressionDiagnostic>(d =>
                d.Code == "ExpressionParser.ExpectedToken" &&
                d.Line == 1 &&
                d.Column == 27);
    }
}
