using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFunctionDateTimeTests
{
    private readonly FormulaEngine _engine = new();

    [Fact]
    public void DATE()
    {
        var result = _engine.Evaluate("=DATE(2024,6,15)", new SpreadsheetSheet());
        result.Should().Be(45458.0); // Excel serial for 2024-06-15
    }

    [Fact]
    public void TIME()
    {
        var result = _engine.Evaluate("=TIME(12,30,0)", new SpreadsheetSheet());
        // 12:30:00 = 12.5 hours / 24 = 0.5208333...
        result.Should().BeOfType<double>().Which.Should().BeApproximately(0.520833, 0.00001);
    }

    [Fact]
    public void YEAR()
    {
        var result = _engine.Evaluate("=YEAR(45458)", new SpreadsheetSheet());
        result.Should().Be(2024.0);
    }

    [Fact]
    public void MONTH()
    {
        var result = _engine.Evaluate("=MONTH(45458)", new SpreadsheetSheet());
        result.Should().Be(6.0);
    }

    [Fact]
    public void DAY()
    {
        var result = _engine.Evaluate("=DAY(45458)", new SpreadsheetSheet());
        result.Should().Be(15.0);
    }

    [Fact]
    public void HOUR()
    {
        var result = _engine.Evaluate("=HOUR(0.75)", new SpreadsheetSheet());
        result.Should().Be(18.0);
    }

    [Fact]
    public void MINUTE()
    {
        var result = _engine.Evaluate("=MINUTE(TIME(12,30,0))", new SpreadsheetSheet());
        result.Should().Be(30.0);
    }

    [Fact]
    public void SECOND()
    {
        var result = _engine.Evaluate("=SECOND(TIME(12,30,0))", new SpreadsheetSheet());
        result.Should().Be(0.0);
    }

    [Fact]
    public void WEEKDAY()
    {
        // 2024-06-15 is Saturday (7 in Excel default, 1=Sunday)
        var result = _engine.Evaluate("=WEEKDAY(45458)", new SpreadsheetSheet());
        result.Should().Be(7.0);
    }

    [Fact]
    public void WEEKNUM()
    {
        var result = _engine.Evaluate("=WEEKNUM(45458)", new SpreadsheetSheet());
        result.Should().Be(24.0);
    }

    [Fact]
    public void DAYS()
    {
        var result = _engine.Evaluate("=DAYS(45458,45196)", new SpreadsheetSheet());
        result.Should().Be(262.0);
    }

    [Fact]
    public void EDATE()
    {
        var result = _engine.Evaluate("=EDATE(45458,1)", new SpreadsheetSheet());
        // 2024-07-15 = 45488
        result.Should().Be(45488.0);
    }

    [Fact]
    public void EOMONTH()
    {
        var result = _engine.Evaluate("=EOMONTH(45458,0)", new SpreadsheetSheet());
        // 2024-06-30 = 45473
        result.Should().Be(45473.0);
    }

    [Fact]
    public void DATEDIF_Days()
    {
        var result = _engine.Evaluate("=DATEDIF(45196,45458,\"D\")", new SpreadsheetSheet());
        result.Should().Be(262.0);
    }

    [Fact]
    public void DATEVALUE()
    {
        var result = _engine.Evaluate("=DATEVALUE(\"2024-06-15\")", new SpreadsheetSheet());
        result.Should().Be(45458.0);
    }

    [Fact]
    public void TIMEVALUE()
    {
        var result = _engine.Evaluate("=TIMEVALUE(\"12:30:00\")", new SpreadsheetSheet());
        result.Should().BeOfType<double>().Which.Should().BeApproximately(0.520833, 0.00001);
    }

    [Fact]
    public void NOW_IsSerial()
    {
        var result = _engine.Evaluate("=NOW()", new SpreadsheetSheet());
        result.Should().BeOfType<double>().Which.Should().BeGreaterThan(45000);
    }

    [Fact]
    public void TODAY_IsWholeNumber()
    {
        var result = _engine.Evaluate("=TODAY()", new SpreadsheetSheet());
        var d = result.Should().BeOfType<double>().Subject;
        d.Should().BeGreaterThan(45000);
        Math.Floor(d).Should().Be(d);
    }
}
