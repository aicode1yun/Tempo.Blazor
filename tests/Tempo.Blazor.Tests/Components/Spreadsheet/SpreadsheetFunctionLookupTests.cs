using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFunctionLookupTests
{
    private readonly FormulaEngine _engine = new();

    private static SpreadsheetSheet CreateLookupSheet()
    {
        var sheet = new SpreadsheetSheet();
        // Column A: IDs
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 1 };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 2 };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = 3 };
        // Column B: Names
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "Alice" };
        sheet.Cells["B2"] = new SpreadsheetCell { Value = "Bob" };
        sheet.Cells["B3"] = new SpreadsheetCell { Value = "Charlie" };
        // Row 5: horizontal data
        sheet.Cells["A5"] = new SpreadsheetCell { Value = "X" };
        sheet.Cells["B5"] = new SpreadsheetCell { Value = "Y" };
        sheet.Cells["C5"] = new SpreadsheetCell { Value = "Z" };
        sheet.Cells["A6"] = new SpreadsheetCell { Value = 10 };
        sheet.Cells["B6"] = new SpreadsheetCell { Value = 20 };
        sheet.Cells["C6"] = new SpreadsheetCell { Value = 30 };
        return sheet;
    }

    [Fact]
    public void VLOOKUP_ExactMatch()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=VLOOKUP(2,A1:B3,2,FALSE)", sheet);
        result.Should().Be("Bob");
    }

    [Fact]
    public void VLOOKUP_ApproximateMatch()
    {
        var sheet = CreateLookupSheet();
        // Approximate match requires sorted data; use exact for simplicity in this test
        var result = _engine.Evaluate("=VLOOKUP(1,A1:B3,2,TRUE)", sheet);
        result.Should().Be("Alice");
    }

    [Fact]
    public void HLOOKUP_ExactMatch()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=HLOOKUP(\"Y\",A5:C6,2,FALSE)", sheet);
        result.Should().Be(20.0);
    }

    [Fact]
    public void INDEX_Range()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=INDEX(A1:B3,2,2)", sheet);
        result.Should().Be("Bob");
    }

    [Fact]
    public void MATCH_Exact()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=MATCH(2,A1:A3,0)", sheet);
        result.Should().Be(2.0);
    }

    [Fact]
    public void CHOOSE()
    {
        var result = _engine.Evaluate("=CHOOSE(2,\"A\",\"B\",\"C\")", new SpreadsheetSheet());
        result.Should().Be("B");
    }

    [Fact]
    public void OFFSET()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=OFFSET(A1,1,1)", sheet);
        result.Should().BeOfType<FormulaError>().Which.Code.Should().Be("#REF!");
    }

    [Fact]
    public void INDIRECT()
    {
        var sheet = CreateLookupSheet();
        var result = _engine.Evaluate("=INDIRECT(\"B2\")", sheet);
        result.Should().Be("Bob");
    }

    [Fact]
    public void ROW()
    {
        var result = _engine.Evaluate("=ROW(B5)", new SpreadsheetSheet());
        result.Should().Be(5.0);
    }

    [Fact]
    public void COLUMN()
    {
        var result = _engine.Evaluate("=COLUMN(B5)", new SpreadsheetSheet());
        result.Should().Be(2.0);
    }

    [Fact]
    public void ROWS()
    {
        var result = _engine.Evaluate("=ROWS(A1:C5)", new SpreadsheetSheet());
        result.Should().Be(5.0);
    }

    [Fact]
    public void COLUMNS()
    {
        var result = _engine.Evaluate("=COLUMNS(A1:C5)", new SpreadsheetSheet());
        result.Should().Be(3.0);
    }

    [Fact]
    public void ADDRESS()
    {
        var result = _engine.Evaluate("=ADDRESS(2,3)", new SpreadsheetSheet());
        result.Should().Be("$C$2");
    }

    [Fact]
    public void ADDRESS_Relative()
    {
        var result = _engine.Evaluate("=ADDRESS(2,3,4)", new SpreadsheetSheet());
        result.Should().Be("C2");
    }
}
