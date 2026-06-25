using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Tests.Expressions;

public sealed class ExpressionLexerTests
{
    [Fact]
    public void Tokenize_RecognizesNumbersStringsIdentifiersOperatorsAndMemberPaths()
    {
        var tokens = ExpressionLexer.Tokenize("=Fields.Price * Parameters.Rate + \"A\\\"B\"").ToList();

        tokens.Select(t => t.Kind).Should().Equal(
        [
            ExpressionTokenKind.Identifier,
            ExpressionTokenKind.Dot,
            ExpressionTokenKind.Identifier,
            ExpressionTokenKind.Star,
            ExpressionTokenKind.Identifier,
            ExpressionTokenKind.Dot,
            ExpressionTokenKind.Identifier,
            ExpressionTokenKind.Plus,
            ExpressionTokenKind.String,
            ExpressionTokenKind.EndOfInput,
        ]);
        tokens[0].Text.Should().Be("Fields");
        tokens[2].Text.Should().Be("Price");
        tokens[8].Value.Should().Be("A\"B");
        tokens[0].Line.Should().Be(1);
        tokens[0].Column.Should().Be(2);
    }

    [Fact]
    public void Tokenize_InvalidCharacter_ReportsLineAndColumn()
    {
        var act = () => ExpressionLexer.Tokenize("=Fields.Price\n + @").ToList();

        act.Should().Throw<ExpressionParseException>()
            .Which.Diagnostic.Should().Match<ExpressionDiagnostic>(d =>
                d.Code == "ExpressionLexer.UnexpectedCharacter" &&
                d.Line == 2 &&
                d.Column == 4);
    }
}
