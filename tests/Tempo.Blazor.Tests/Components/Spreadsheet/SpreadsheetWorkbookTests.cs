using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetWorkbookTests
{
    [Fact]
    public void Constructor_CreatesDefaultSheet()
    {
        var workbook = new SpreadsheetWorkbook();

        workbook.Sheets.Should().HaveCount(1);
        workbook.ActiveSheet.Should().NotBeNull();
        workbook.ActiveSheet!.Name.Should().Be("Sheet1");
        workbook.ActiveSheetIndex.Should().Be(0);
    }

    [Fact]
    public void AddSheet_AddsNewSheet()
    {
        var workbook = new SpreadsheetWorkbook();

        var sheet = workbook.AddSheet("Sheet2");

        workbook.Sheets.Should().HaveCount(2);
        sheet.Name.Should().Be("Sheet2");
    }

    [Fact]
    public void RemoveSheet_ByIndex_RemovesSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.AddSheet("Sheet2");

        workbook.RemoveSheet(0);

        workbook.Sheets.Should().HaveCount(1);
        workbook.ActiveSheet!.Name.Should().Be("Sheet2");
    }

    [Fact]
    public void RemoveSheet_ByName_RemovesSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.AddSheet("Sheet2");

        workbook.RemoveSheet("Sheet1");

        workbook.Sheets.Should().HaveCount(1);
        workbook.ActiveSheet!.Name.Should().Be("Sheet2");
    }

    [Fact]
    public void RemoveSheet_LastSheet_AdjustsActiveIndex()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.AddSheet("Sheet2");
        workbook.ActiveSheetIndex = 1;

        workbook.RemoveSheet(1);

        workbook.ActiveSheetIndex.Should().Be(0);
    }

    [Fact]
    public void RemoveSheet_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        var workbook = new SpreadsheetWorkbook();

        Action act = () => workbook.RemoveSheet(5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RemoveSheet_InvalidName_ThrowsArgumentException()
    {
        var workbook = new SpreadsheetWorkbook();

        Action act = () => workbook.RemoveSheet("NonExistent");

        act.Should().Throw<ArgumentException>().WithMessage("*Sheet 'NonExistent' not found.*");
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new SpreadsheetWorkbook();
        original.ActiveSheet!.SetCellValue(0, 0, "Hello");

        var clone = original.Clone();

        clone.Sheets.Should().HaveCount(1);
        clone.ActiveSheet!.Cells["A1"].Value.Should().Be("Hello");

        // Modify clone and verify original is unaffected
        clone.ActiveSheet.SetCellValue(0, 0, "World");

        original.ActiveSheet.Cells["A1"].Value.Should().Be("Hello");
    }
}
