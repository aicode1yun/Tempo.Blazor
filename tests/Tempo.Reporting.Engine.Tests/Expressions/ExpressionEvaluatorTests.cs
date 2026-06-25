using System.Diagnostics;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Tests.Expressions;

public sealed class ExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_UsesContextDictionariesGlobalsAndDeferredPagePlaceholders()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?> { ["Price"] = 12.5m, ["Quantity"] = 4 },
            parameters: new Dictionary<string, object?> { ["Rate"] = 1.2m },
            globals: new ExpressionGlobals
            {
                ExecutionTime = new DateTimeOffset(2026, 6, 22, 8, 30, 0, TimeSpan.Zero),
                UserName = "ada",
                TenantName = "northwind",
            });

        ExpressionEvaluator.Evaluate("=Fields.Price * Fields.Quantity * Parameters.Rate", context)
            .AsNumber().Should().Be(60m);
        ExpressionEvaluator.Evaluate("=Globals.UserName + \"@\" + Globals.TenantName", context)
            .AsString().Should().Be("ada@northwind");

        var pageNumber = ExpressionEvaluator.Evaluate("=Globals.PageNumber", context);

        pageNumber.Kind.Should().Be(ExpressionValueKind.Deferred);
        pageNumber.DeferredKind.Should().Be(ExpressionDeferredKind.PageNumber);
    }

    [Fact]
    public void Evaluate_CoercesTypesAndPropagatesNull()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?> { ["TextNumber"] = "10.5", ["Missing"] = null },
            parameters: new Dictionary<string, object?> { ["Flag"] = "true" });

        ExpressionEvaluator.Evaluate("=Fields.TextNumber + 2", context).AsNumber().Should().Be(12.5m);
        ExpressionEvaluator.Evaluate("=Parameters.Flag and true", context).AsBoolean().Should().BeTrue();
        ExpressionEvaluator.Evaluate("=Fields.Missing * 2", context).Kind.Should().Be(ExpressionValueKind.Null);
    }

    [Fact]
    public void Evaluate_BuiltInFunctions_CoverMathStringDateLogicalAndConversionCases()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?>
            {
                ["Name"] = "  ada  ",
                ["When"] = new DateTime(2026, 6, 22),
                ["Blank"] = null,
            },
            parameters: new Dictionary<string, object?>());

        ExpressionEvaluator.Evaluate("=Abs(-3) + Round(1.6)", context).AsNumber().Should().Be(5m);
        ExpressionEvaluator.Evaluate("=Upper(Trim(Fields.Name))", context).AsString().Should().Be("ADA");
        ExpressionEvaluator.Evaluate("=Format(Fields.When, \"dd.MM.yyyy\")", context).AsString().Should().Be("22.06.2026");
        ExpressionEvaluator.Evaluate("=Year(AddDays(Fields.When, 10))", context).AsNumber().Should().Be(2026m);
        ExpressionEvaluator.Evaluate("=IIf(IsNull(Fields.Blank), \"fallback\", Unknown.Root)", context).AsString().Should().Be("fallback");
        ExpressionEvaluator.Evaluate("=Switch(false, \"no\", 1 = 1, \"yes\", \"fallback\")", context).AsString().Should().Be("yes");
        ExpressionEvaluator.Evaluate("=CDate(\"2026-06-22\").Year", context).AsNumber().Should().Be(2026m);
        ExpressionEvaluator.Evaluate("=CBool(\"true\") and (CDec(\"2.5\") = 2.5)", context).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RejectsAccessOutsideExplicitContextWithoutReflection()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?>(),
            parameters: new Dictionary<string, object?>());

        var act = () => ExpressionEvaluator.Evaluate("=System.Environment.UserName", context);

        act.Should().Throw<ExpressionEvaluationException>()
            .Which.Diagnostic.Code.Should().Be("ExpressionEvaluator.UnknownRoot");
    }

    [Fact]
    public void Evaluate_IsDeterministicForSameContext()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?> { ["Price"] = 5 },
            parameters: new Dictionary<string, object?> { ["Rate"] = 3 });
        var expression = ExpressionParser.Parse("=Fields.Price * Parameters.Rate + 1");

        var first = ExpressionEvaluator.Evaluate(expression, context);
        var second = ExpressionEvaluator.Evaluate(expression, context);

        second.Should().Be(first);
    }

    [Fact]
    public void Evaluate_StopsWhenEvaluationBudgetIsExceeded()
    {
        var context = new ExpressionContext(
            fields: new Dictionary<string, object?> { ["Price"] = 5 },
            parameters: new Dictionary<string, object?>());

        var act = () => ExpressionEvaluator.Evaluate(
            "=Fields.Price + 1",
            context,
            new ExpressionEvaluationOptions { MaxEvaluationSteps = 1 });

        act.Should().Throw<ExpressionEvaluationException>()
            .Which.Diagnostic.Code.Should().Be("ExpressionEvaluator.Timeout");
    }
}
