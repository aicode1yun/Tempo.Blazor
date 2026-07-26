using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Reference tests for the pure document-assembly expression evaluator: conditions over token
/// values, arithmetic, SUM over collection rows, currency formatting and date arithmetic.
/// Deterministic — "now" is injected through the context.
/// </summary>
public class DocumentAssemblyExpressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);

    // ── Literals, identifiers, arithmetic ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("7 - 10", "-3")]
    public void Evaluates_Arithmetic(string expression, string expected)
        => Evaluate(expression).ToInvariantString().Should().Be(expected);

    [Fact]
    public void Resolves_TokenIdentifiers_AsNumbersWhenNumeric()
        => Evaluate("amount * 2", Values(("amount", "1250.50"))).ToInvariantString().Should().Be("2501");

    [Fact]
    public void Resolves_TokenIdentifiers_AsStrings()
        => Evaluate("customerName", Values(("customerName", "Novák s.r.o."))).ToInvariantString().Should().Be("Novák s.r.o.");

    // ── Conditions ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("amount > 10000", "15000", true)]
    [InlineData("amount > 10000", "9000", false)]
    [InlineData("amount >= 10000", "10000", true)]
    [InlineData("amount == 10000", "10000", true)]
    [InlineData("amount != 10000", "10000", false)]
    public void Evaluates_NumericComparisons(string expression, string amount, bool expected)
        => Evaluate(expression, Values(("amount", amount))).ToBoolean().Should().Be(expected);

    [Theory]
    [InlineData("type == 'lease'", "lease", true)]
    [InlineData("type == 'lease'", "sale", false)]
    [InlineData("type != 'lease'", "sale", true)]
    public void Evaluates_StringComparisons(string expression, string type, bool expected)
        => Evaluate(expression, Values(("type", type))).ToBoolean().Should().Be(expected);

    [Theory]
    [InlineData("amount > 1000 && type == 'lease'", true)]
    [InlineData("amount > 99999 || type == 'lease'", true)]
    [InlineData("!(type == 'lease')", false)]
    public void Evaluates_BooleanOperators(string expression, bool expected)
        => Evaluate(expression, Values(("amount", "5000"), ("type", "lease"))).ToBoolean().Should().Be(expected);

    [Fact]
    public void MissingToken_EvaluatesAsEmpty_AndComparesFalseToNumbers()
        => Evaluate("missing > 0").ToBoolean().Should().BeFalse();

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Resolves_BooleanTokenValues_AsBooleans(string rawValue, bool expected)
        => Evaluate("enabled", Values(("enabled", rawValue))).ToBoolean().Should().Be(expected);

    // ── SUM over collection rows ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sum_AddsNumericColumnAcrossRows()
    {
        var values = new Dictionary<string, DocumentTokenValue>
        {
            ["items"] = new()
            {
                Key = "items",
                Rows =
                [
                    new Dictionary<string, string?> { ["price"] = "1000", ["name"] = "A" },
                    new Dictionary<string, string?> { ["price"] = "250.75", ["name"] = "B" },
                    new Dictionary<string, string?> { ["price"] = "49.25", ["name"] = "C" },
                ],
            },
        };

        Evaluate("SUM(items, 'price')", values).ToInvariantString().Should().Be("1300");
    }

    [Fact]
    public void Count_ReturnsRowCount()
    {
        var values = new Dictionary<string, DocumentTokenValue>
        {
            ["items"] = new() { Key = "items", Rows = [new(), new(), new()] },
        };

        Evaluate("COUNT(items)", values).ToInvariantString().Should().Be("3");
    }

    // ── Formatting ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Currency_FormatsWithCulture()
        => Evaluate("CURRENCY(amount, 'cs-CZ', 'CZK')", Values(("amount", "1250.5")))
            .ToInvariantString().Should().Contain("1").And.Contain("250,50").And.Contain("Kč");

    [Fact]
    public void Format_FormatsNumbersInvariantByDefault()
        => Evaluate("FORMAT(amount, 'N2')", Values(("amount", "1250.5")))
            .ToInvariantString().Should().Be("1,250.50");

    // ── Date arithmetic ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DateAdd_AddsDaysToIsoDate()
        => Evaluate("DATEADD(signedOn, 30)", Values(("signedOn", "2026-07-01")))
            .ToInvariantString().Should().Be("2026-07-31");

    [Fact]
    public void Today_ComesFromInjectedContextClock()
        => Evaluate("TODAY()").ToInvariantString().Should().Be("2026-07-18");

    [Fact]
    public void DateAdd_OverToday_IsDeterministic()
        => Evaluate("DATEADD(TODAY(), 14)").ToInvariantString().Should().Be("2026-08-01");

    // ── Errors ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1 +")]
    [InlineData("SUM(items")]
    [InlineData("??")]
    public void InvalidExpression_ThrowsFormatException(string expression)
    {
        var act = () => Evaluate(expression);

        act.Should().Throw<FormatException>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DocumentAssemblyValue Evaluate(
        string expression,
        IReadOnlyDictionary<string, DocumentTokenValue>? values = null)
        => DocumentAssemblyExpression.Evaluate(expression, new DocumentAssemblyContext
        {
            TokenValues = values ?? new Dictionary<string, DocumentTokenValue>(),
            Now = Now,
        });

    private static Dictionary<string, DocumentTokenValue> Values(params (string Key, string Value)[] pairs)
        => pairs.ToDictionary(
            pair => pair.Key,
            pair => new DocumentTokenValue { Key = pair.Key, Value = pair.Value });
}
