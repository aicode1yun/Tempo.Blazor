using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetSearchEngineTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static SpreadsheetSheet SheetWith(params (string Ref, string Display)[] cells)
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        foreach (var (cellRef, display) in cells)
            sheet.Cells[cellRef] = new SpreadsheetCell { Value = display, DisplayValue = display };
        return sheet;
    }

    [Fact]
    public void FindInSheet_Substring_ReturnsHit()
    {
        var sheet = SheetWith(("A1", "Hello World"), ("B2", "Goodbye"));
        var options = new SpreadsheetSearchOptions { Query = "World" };

        var hits = SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture);

        hits.Should().HaveCount(1);
        hits[0].CellRef.Should().Be("A1");
        hits[0].MatchStart.Should().Be(6);
        hits[0].MatchLength.Should().Be(5);
    }

    [Fact]
    public void FindInSheet_EmptyQuery_ReturnsNothing()
    {
        var sheet = SheetWith(("A1", "Hello"));
        var options = new SpreadsheetSearchOptions { Query = "" };

        SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture).Should().BeEmpty();
    }

    [Fact]
    public void FindInSheet_CaseInsensitiveByDefault()
    {
        var sheet = SheetWith(("A1", "Hello"));
        var options = new SpreadsheetSearchOptions { Query = "hello" };

        SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture).Should().HaveCount(1);
    }

    [Fact]
    public void FindInSheet_MatchCase_Respected()
    {
        var sheet = SheetWith(("A1", "Hello"), ("A2", "hello"));
        var options = new SpreadsheetSearchOptions { Query = "hello", MatchCase = true };

        var hits = SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture);

        hits.Should().HaveCount(1);
        hits[0].CellRef.Should().Be("A2");
    }

    [Fact]
    public void FindInSheet_WholeCell_OnlyExactMatches()
    {
        var sheet = SheetWith(("A1", "cat"), ("A2", "category"));
        var options = new SpreadsheetSearchOptions { Query = "cat", WholeCell = true };

        var hits = SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture);

        hits.Should().HaveCount(1);
        hits[0].CellRef.Should().Be("A1");
        hits[0].MatchLength.Should().Be(3);
    }

    [Fact]
    public void FindInSheet_OrdersByRowThenColumn()
    {
        var sheet = SheetWith(("B1", "x"), ("A1", "x"), ("A2", "x"));
        var options = new SpreadsheetSearchOptions { Query = "x" };

        var hits = SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture);

        hits.Select(h => h.CellRef).Should().ContainInOrder("A1", "B1", "A2");
    }

    [Fact]
    public void FindInSheet_Values_DoesNotMatchFormulaText()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        sheet.Cells["A1"] = new SpreadsheetCell { Formula = "=SUM(B1:B2)", Value = 30.0, DisplayValue = "30" };
        var options = new SpreadsheetSearchOptions { Query = "SUM", SearchIn = SpreadsheetSearchIn.Values };

        SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture).Should().BeEmpty();
    }

    [Fact]
    public void FindInSheet_Formulas_MatchesFormulaText()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        sheet.Cells["A1"] = new SpreadsheetCell { Formula = "=SUM(B1:B2)", Value = 30.0, DisplayValue = "30" };
        var options = new SpreadsheetSearchOptions { Query = "SUM", SearchIn = SpreadsheetSearchIn.Formulas };

        var hits = SpreadsheetSearchEngine.FindInSheet(sheet, 0, options, Culture);

        hits.Should().HaveCount(1);
        hits[0].CellRef.Should().Be("A1");
        hits[0].MatchStart.Should().Be(1); // after the leading '='
    }

    [Fact]
    public void Find_WorkbookScope_SearchesAllSheets()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].Name = "First";
        workbook.Sheets[0].Cells["A1"] = new SpreadsheetCell { Value = "target", DisplayValue = "target" };
        var second = workbook.AddSheet("Second");
        second.Cells["C3"] = new SpreadsheetCell { Value = "target", DisplayValue = "target" };

        var options = new SpreadsheetSearchOptions { Query = "target", Scope = SpreadsheetSearchScope.Workbook };
        var hits = SpreadsheetSearchEngine.Find(workbook, 0, options, Culture);

        hits.Should().HaveCount(2);
        hits[0].SheetIndex.Should().Be(0);
        hits[1].SheetIndex.Should().Be(1);
        hits[1].SheetName.Should().Be("Second");
    }

    [Fact]
    public void Find_SheetScope_OnlySearchesActiveSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].Cells["A1"] = new SpreadsheetCell { Value = "target", DisplayValue = "target" };
        var second = workbook.AddSheet("Second");
        second.Cells["C3"] = new SpreadsheetCell { Value = "target", DisplayValue = "target" };

        var options = new SpreadsheetSearchOptions { Query = "target", Scope = SpreadsheetSearchScope.Sheet };
        var hits = SpreadsheetSearchEngine.Find(workbook, 1, options, Culture);

        hits.Should().HaveCount(1);
        hits[0].SheetIndex.Should().Be(1);
        hits[0].CellRef.Should().Be("C3");
    }
}
