using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class NamedRangeCommandTests
{
    [Fact]
    public void AddNamedRangeCommand_AddsRange_AndRecalculatesDependents()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 5);
        workbook.Sheets[0].SetCellValue(0, 1, 10);
        workbook.Sheets[0].SetCellFormula(1, 0, "=SUM(Values)"); // A2

        var range = new SpreadsheetNamedRange { Name = "Values", RefersTo = "A1:B1" };
        var cmd = new AddNamedRangeCommand(workbook, range);

        cmd.Execute();

        workbook.NamedRanges.Should().ContainSingle();
        workbook.Sheets[0].Cells["A2"].Value.Should().Be(15.0);

        cmd.Undo();

        workbook.NamedRanges.Should().BeEmpty();
        workbook.Sheets[0].Cells["A2"].Value.Should().BeOfType<FormulaError>().Which.Code.Should().Be("#NAME?");
    }

    [Fact]
    public void DeleteNamedRangeCommand_RemovesRange_AndRecalculatesDependents()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 5);
        workbook.Sheets[0].SetCellValue(0, 1, 10);

        var range = new SpreadsheetNamedRange { Name = "Values", RefersTo = "A1:B1" };
        workbook.NamedRanges.Add(range);
        workbook.Sheets[0].SetCellFormula(1, 0, "=SUM(Values)"); // A2

        var cmd = new DeleteNamedRangeCommand(workbook, range);
        cmd.Execute();

        workbook.NamedRanges.Should().BeEmpty();
        workbook.Sheets[0].Cells["A2"].Value.Should().BeOfType<FormulaError>().Which.Code.Should().Be("#NAME?");

        cmd.Undo();

        workbook.NamedRanges.Should().ContainSingle();
        workbook.Sheets[0].Cells["A2"].Value.Should().Be(15.0);
    }

    [Fact]
    public void EditNamedRangeCommand_UpdatesRange_AndRecalculatesDependents()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 5);
        workbook.Sheets[0].SetCellValue(0, 1, 10);
        workbook.Sheets[0].SetCellValue(0, 2, 20);

        var range = new SpreadsheetNamedRange { Name = "Values", RefersTo = "A1:B1" };
        workbook.NamedRanges.Add(range);
        workbook.Sheets[0].SetCellFormula(1, 0, "=SUM(Values)"); // A2

        var cmd = new EditNamedRangeCommand(workbook, range, "Values", "C1", NamedRangeScope.Workbook, null, null);
        cmd.Execute();

        workbook.NamedRanges[0].RefersTo.Should().Be("C1");
        workbook.Sheets[0].Cells["A2"].Value.Should().Be(20.0);

        cmd.Undo();

        workbook.NamedRanges[0].RefersTo.Should().Be("A1:B1");
        workbook.Sheets[0].Cells["A2"].Value.Should().Be(15.0);
    }

    [Fact]
    public void EditNamedRangeCommand_Rename_RecalculatesOldAndNewName()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 5);
        workbook.Sheets[0].SetCellFormula(1, 0, "=SUM(Values)");

        var range = new SpreadsheetNamedRange { Name = "Values", RefersTo = "A1" };
        workbook.NamedRanges.Add(range);
        workbook.RecalculateNamedRangeDependents("Values");

        var cmd = new EditNamedRangeCommand(workbook, range, "Total", "A1", NamedRangeScope.Workbook, null, null);
        cmd.Execute();

        workbook.Sheets[0].Cells["A2"].Value.Should().BeOfType<FormulaError>().Which.Code.Should().Be("#NAME?");

        cmd.Undo();

        workbook.Sheets[0].Cells["A2"].Value.Should().Be(5.0);
    }
}
