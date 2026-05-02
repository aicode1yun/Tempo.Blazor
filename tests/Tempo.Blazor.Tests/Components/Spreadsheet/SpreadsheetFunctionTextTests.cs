using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFunctionTextTests
{
    private readonly FormulaEngine _engine = new();

    [Theory]
    [InlineData("=CONCATENATE(\"Hello\",\" \",\"World\")", "Hello World")]
    [InlineData("=CONCATENATE(\"A\",\"B\",\"C\")", "ABC")]
    public void CONCATENATE(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=LEFT(\"Hello\",2)", "He")]
    [InlineData("=LEFT(\"Hello\",10)", "Hello")]
    [InlineData("=LEFT(\"Hello\",0)", "")]
    public void LEFT(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=RIGHT(\"Hello\",2)", "lo")]
    [InlineData("=RIGHT(\"Hello\",10)", "Hello")]
    [InlineData("=RIGHT(\"Hello\",0)", "")]
    public void RIGHT(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=MID(\"Hello World\",7,5)", "World")]
    [InlineData("=MID(\"Hello\",2,2)", "el")]
    [InlineData("=MID(\"Hello\",10,5)", "")]
    public void MID(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=LEN(\"Hello\")", 5.0)]
    [InlineData("=LEN(\"\")", 0.0)]
    [InlineData("=LEN(\"Hello World\")", 11.0)]
    public void LEN(string formula, double expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=TRIM(\"  Hello  World  \")", "Hello World")]
    [InlineData("=TRIM(\"Hello\")", "Hello")]
    [InlineData("=TRIM(\"   \")", "")]
    public void TRIM(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=UPPER(\"hello\")", "HELLO")]
    [InlineData("=UPPER(\"Hello World\")", "HELLO WORLD")]
    public void UPPER(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=LOWER(\"HELLO\")", "hello")]
    [InlineData("=LOWER(\"Hello World\")", "hello world")]
    public void LOWER(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=PROPER(\"hello world\")", "Hello World")]
    [InlineData("=PROPER(\"HELLO\")", "Hello")]
    [InlineData("=PROPER(\"hello-world test\")", "Hello-World Test")]
    public void PROPER(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Fact]
    public void TEXT_Number()
    {
        var result = _engine.Evaluate("=TEXT(1234.56,\"#,##0.00\")", new SpreadsheetSheet());
        result.Should().Be("1,234.56");
    }

    [Fact]
    public void TEXT_Date()
    {
        // Excel serial 45458 = 2024-06-15
        var result = _engine.Evaluate("=TEXT(45458,\"yyyy-MM-dd\")", new SpreadsheetSheet());
        result.Should().Be("2024-06-15");
    }

    [Theory]
    [InlineData("=VALUE(\"123\")", 123.0)]
    [InlineData("=VALUE(\"3.14\")", 3.14)]
    public void VALUE(string formula, double expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=FIND(\"o\",\"Hello\")", 5.0)]
    [InlineData("=FIND(\"l\",\"Hello\",4)", 4.0)]
    public void FIND(string formula, double expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=SEARCH(\"O\",\"Hello\")", 5.0)]
    [InlineData("=SEARCH(\"L\",\"Hello\",4)", 4.0)]
    public void SEARCH(string formula, double expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=SUBSTITUTE(\"Hello World\",\"World\",\"Universe\")", "Hello Universe")]
    [InlineData("=SUBSTITUTE(\"ababab\",\"a\",\"c\",2)", "abcbab")]
    public void SUBSTITUTE(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=REPT(\"*\",5)", "*****")]
    [InlineData("=REPT(\"ab\",3)", "ababab")]
    public void REPT(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }
}
