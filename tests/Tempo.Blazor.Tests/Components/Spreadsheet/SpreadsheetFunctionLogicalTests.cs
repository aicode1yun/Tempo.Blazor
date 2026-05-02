using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFunctionLogicalTests
{
    private readonly FormulaEngine _engine = new();

    private static SpreadsheetSheet SheetWithValues(Dictionary<string, object> values)
    {
        var sheet = new SpreadsheetSheet();
        foreach (var kv in values)
            sheet.Cells[kv.Key] = new SpreadsheetCell { Value = kv.Value };
        return sheet;
    }

    [Theory]
    [InlineData("=IF(TRUE,1,2)", 1.0)]
    [InlineData("=IF(FALSE,1,2)", 2.0)]
    [InlineData("=IF(5>3,\"yes\",\"no\")", "yes")]
    [InlineData("=IF(5<3,\"yes\",\"no\")", "no")]
    public void IF(string formula, object expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=AND(TRUE,TRUE)", true)]
    [InlineData("=AND(TRUE,FALSE)", false)]
    [InlineData("=AND(1,1,1)", true)]
    [InlineData("=AND(1,0,1)", false)]
    public void AND(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=OR(FALSE,FALSE)", false)]
    [InlineData("=OR(TRUE,FALSE)", true)]
    [InlineData("=OR(0,0,0)", false)]
    [InlineData("=OR(0,1,0)", true)]
    public void OR(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=NOT(TRUE)", false)]
    [InlineData("=NOT(FALSE)", true)]
    [InlineData("=NOT(1)", false)]
    [InlineData("=NOT(0)", true)]
    public void NOT(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Fact]
    public void IFERROR_Value()
    {
        var result = _engine.Evaluate("=IFERROR(10/2,\"err\")", new SpreadsheetSheet());
        result.Should().Be(5.0);
    }

    [Fact]
    public void IFERROR_Error()
    {
        var result = _engine.Evaluate("=IFERROR(10/0,\"err\")", new SpreadsheetSheet());
        result.Should().Be("err");
    }

    [Theory]
    [InlineData("=ISBLANK(A1)", true)]
    [InlineData("=ISBLANK(A2)", false)]
    public void ISBLANK(string formula, bool expected)
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 42 };
        var result = _engine.Evaluate(formula, sheet);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISNUMBER(123)", true)]
    [InlineData("=ISNUMBER(\"text\")", false)]
    public void ISNUMBER(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISTEXT(\"hello\")", true)]
    [InlineData("=ISTEXT(123)", false)]
    public void ISTEXT(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISERROR(10/0)", true)]
    [InlineData("=ISERROR(10/2)", false)]
    public void ISERROR(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISLOGICAL(TRUE)", true)]
    [InlineData("=ISLOGICAL(FALSE)", true)]
    [InlineData("=ISLOGICAL(1)", false)]
    public void ISLOGICAL(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISEVEN(2)", true)]
    [InlineData("=ISEVEN(3)", false)]
    [InlineData("=ISEVEN(0)", true)]
    public void ISEVEN(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("=ISODD(3)", true)]
    [InlineData("=ISODD(2)", false)]
    [InlineData("=ISODD(0)", false)]
    public void ISODD(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, new SpreadsheetSheet());
        result.Should().Be(expected);
    }
}
