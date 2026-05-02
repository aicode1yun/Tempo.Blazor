using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaEvaluatorTests
{
    private readonly FormulaEngine _engine = new();

    private static SpreadsheetSheet CreateSheetWithValues(Dictionary<string, object> values)
    {
        var sheet = new SpreadsheetSheet();
        foreach (var kv in values)
        {
            sheet.Cells[kv.Key] = new SpreadsheetCell { Value = kv.Value };
        }
        return sheet;
    }

    [Fact]
    public void Evaluate_NumberLiteral()
    {
        var result = _engine.Evaluate("=42", new SpreadsheetSheet());
        result.Should().Be(42.0);
    }

    [Fact]
    public void Evaluate_Addition()
    {
        var result = _engine.Evaluate("=1+2", new SpreadsheetSheet());
        result.Should().Be(3.0);
    }

    [Fact]
    public void Evaluate_Subtraction()
    {
        var result = _engine.Evaluate("=5-3", new SpreadsheetSheet());
        result.Should().Be(2.0);
    }

    [Fact]
    public void Evaluate_Multiplication()
    {
        var result = _engine.Evaluate("=4*5", new SpreadsheetSheet());
        result.Should().Be(20.0);
    }

    [Fact]
    public void Evaluate_Division()
    {
        var result = _engine.Evaluate("=10/2", new SpreadsheetSheet());
        result.Should().Be(5.0);
    }

    [Fact]
    public void Evaluate_Power()
    {
        var result = _engine.Evaluate("=2^3", new SpreadsheetSheet());
        result.Should().Be(8.0);
    }

    [Fact]
    public void Evaluate_Percent()
    {
        var result = _engine.Evaluate("=50%", new SpreadsheetSheet());
        result.Should().Be(0.5);
    }

    [Fact]
    public void Evaluate_UnaryMinus()
    {
        var result = _engine.Evaluate("=-5", new SpreadsheetSheet());
        result.Should().Be(-5.0);
    }

    [Fact]
    public void Evaluate_Precedence()
    {
        var result = _engine.Evaluate("=1+2*3", new SpreadsheetSheet());
        result.Should().Be(7.0);
    }

    [Fact]
    public void Evaluate_Parentheses()
    {
        var result = _engine.Evaluate("=(1+2)*3", new SpreadsheetSheet());
        result.Should().Be(9.0);
    }

    [Fact]
    public void Evaluate_Comparison_Equal()
    {
        var result = _engine.Evaluate("=5=5", new SpreadsheetSheet());
        result.Should().Be(true);
    }

    [Fact]
    public void Evaluate_Comparison_NotEqual()
    {
        var result = _engine.Evaluate("=5<>3", new SpreadsheetSheet());
        result.Should().Be(true);
    }

    [Fact]
    public void Evaluate_Comparison_GreaterThan()
    {
        var result = _engine.Evaluate("=5>3", new SpreadsheetSheet());
        result.Should().Be(true);
    }

    [Fact]
    public void Evaluate_Comparison_LessThanOrEqual()
    {
        var result = _engine.Evaluate("=3<=5", new SpreadsheetSheet());
        result.Should().Be(true);
    }

    [Fact]
    public void Evaluate_CellRef()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 42 });
        var result = _engine.Evaluate("=A1", sheet);
        result.Should().Be(42.0);
    }

    [Fact]
    public void Evaluate_CellRef_Addition()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 10, ["B1"] = 20 });
        var result = _engine.Evaluate("=A1+B1", sheet);
        result.Should().Be(30.0);
    }

    [Fact]
    public void Evaluate_SUM()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 1, ["A2"] = 2, ["A3"] = 3 });
        var result = _engine.Evaluate("=SUM(A1:A3)", sheet);
        result.Should().Be(6.0);
    }

    [Fact]
    public void Evaluate_AVERAGE()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 10, ["A2"] = 20, ["A3"] = 30 });
        var result = _engine.Evaluate("=AVERAGE(A1:A3)", sheet);
        result.Should().Be(20.0);
    }

    [Fact]
    public void Evaluate_COUNT()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 1, ["A2"] = 2 });
        var result = _engine.Evaluate("=COUNT(A1:A2)", sheet);
        result.Should().Be(2.0);
    }

    [Fact]
    public void Evaluate_MIN()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 5, ["A2"] = 2, ["A3"] = 8 });
        var result = _engine.Evaluate("=MIN(A1:A3)", sheet);
        result.Should().Be(2.0);
    }

    [Fact]
    public void Evaluate_MAX()
    {
        var sheet = CreateSheetWithValues(new() { ["A1"] = 5, ["A2"] = 2, ["A3"] = 8 });
        var result = _engine.Evaluate("=MAX(A1:A3)", sheet);
        result.Should().Be(8.0);
    }

    [Fact]
    public void Evaluate_ABS()
    {
        var result = _engine.Evaluate("=ABS(-10)", new SpreadsheetSheet());
        result.Should().Be(10.0);
    }

    [Fact]
    public void Evaluate_ROUND()
    {
        var result = _engine.Evaluate("=ROUND(3.14159,2)", new SpreadsheetSheet());
        result.Should().Be(3.14);
    }

    [Fact]
    public void Evaluate_ROUNDDOWN()
    {
        var result = _engine.Evaluate("=ROUNDDOWN(3.9,0)", new SpreadsheetSheet());
        result.Should().Be(3.0);
    }

    [Fact]
    public void Evaluate_ROUNDUP()
    {
        var result = _engine.Evaluate("=ROUNDUP(3.1,0)", new SpreadsheetSheet());
        result.Should().Be(4.0);
    }

    [Fact]
    public void Evaluate_MOD()
    {
        var result = _engine.Evaluate("=MOD(10,3)", new SpreadsheetSheet());
        result.Should().Be(1.0);
    }

    [Fact]
    public void Evaluate_POWER()
    {
        var result = _engine.Evaluate("=POWER(2,3)", new SpreadsheetSheet());
        result.Should().Be(8.0);
    }

    [Fact]
    public void Evaluate_SQRT()
    {
        var result = _engine.Evaluate("=SQRT(16)", new SpreadsheetSheet());
        result.Should().Be(4.0);
    }

    [Fact]
    public void Evaluate_PI()
    {
        var result = _engine.Evaluate("=PI()", new SpreadsheetSheet());
        result.Should().Be(Math.PI);
    }

    [Fact]
    public void Evaluate_RANDBETWEEN()
    {
        var result = _engine.Evaluate("=RANDBETWEEN(1,10)", new SpreadsheetSheet());
        var val = Convert.ToInt32(result);
        val.Should().BeInRange(1, 10);
    }

    [Fact]
    public void Evaluate_StringConcatenation()
    {
        var result = _engine.Evaluate("=\"Hello\"&\" World\"", new SpreadsheetSheet());
        result.Should().Be("Hello World");
    }
}
