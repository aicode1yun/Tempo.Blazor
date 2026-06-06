using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaNamedRangeTests
{
    [Fact]
    public void Evaluate_ResolvesWorkbookNamedRange()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 10); // A1
        workbook.Sheets[0].SetCellValue(1, 0, 20); // A2
        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "Values",
            RefersTo = "A1:A2",
            Scope = NamedRangeScope.Workbook
        });

        var engine = new FormulaEngine();
        engine.Evaluate("=SUM(Values)", workbook.Sheets[0], workbook, 0).Should().Be(30.0);
    }

    [Fact]
    public void Evaluate_SheetScopeTakesPrecedenceOnOwnSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.AddSheet("Sheet2");
        workbook.Sheets[0].SetCellValue(0, 0, 10);
        workbook.Sheets[1].SetCellValue(0, 0, 99);

        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "Total",
            RefersTo = "A1",
            Scope = NamedRangeScope.Workbook
        });
        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "Total",
            RefersTo = "Sheet2!A1",
            Scope = NamedRangeScope.Sheet,
            SheetIndex = 1
        });

        var engine = new FormulaEngine();
        engine.Evaluate("=Total", workbook.Sheets[0], workbook, 0).Should().Be(10.0);
        engine.Evaluate("=Total", workbook.Sheets[1], workbook, 1).Should().Be(99.0);
    }

    [Fact]
    public void Evaluate_InvalidNamedRange_ReturnsNameError()
    {
        var workbook = new SpreadsheetWorkbook();
        var engine = new FormulaEngine();
        engine.Evaluate("=SUM(MissingName)", workbook.Sheets[0], workbook, 0)
            .Should().BeOfType<FormulaError>().Which.Code.Should().Be("#NAME?");
    }

    [Fact]
    public void DependencyExtractor_ExtractsNamedRanges()
    {
        var refs = FormulaDependencyExtractor.ExtractCellRefs("=SUM(Values)");
        refs.Should().BeEmpty();

        var names = FormulaDependencyExtractor.ExtractNamedRanges("=SUM(Values)");
        names.Should().ContainSingle().Which.Should().Be("Values");
    }

    [Fact]
    public void NamedRange_InvalidatesDependents_WhenContentChanges()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets[0].SetCellValue(0, 0, 10);
        workbook.Sheets[0].SetCellValue(0, 1, 20);
        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "Values",
            RefersTo = "A1:B1",
            Scope = NamedRangeScope.Workbook
        });

        var engine = new FormulaEngine();
        workbook.Sheets[0].SetCellFormula(1, 0, "=SUM(Values)"); // A2

        workbook.Sheets[0].Cells["A2"].Value.Should().Be(30.0);

        // Change the named range to point elsewhere
        workbook.NamedRanges[0].RefersTo = "B1";
        workbook.Sheets[0].RecalculateDependents("Values");

        engine.Evaluate(workbook.Sheets[0].Cells["A2"].Formula!, workbook.Sheets[0], workbook, 0)
            .Should().Be(20.0);
    }
}
