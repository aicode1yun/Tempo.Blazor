using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaDependencyTests
{
    [Fact]
    public void ExtractDependencies_CellRef()
    {
        var deps = FormulaDependencyExtractor.ExtractCellRefs("=A1");
        deps.Should().Contain("A1");
    }

    [Fact]
    public void ExtractDependencies_RangeRef_ExpandsToIndividualCells()
    {
        var deps = FormulaDependencyExtractor.ExtractCellRefs("=SUM(A1:B2)");
        deps.Should().BeEquivalentTo(new[] { "A1", "A2", "B1", "B2" });
    }

    [Fact]
    public void ExtractDependencies_MultipleRefs()
    {
        var deps = FormulaDependencyExtractor.ExtractCellRefs("=A1+B1*C1");
        deps.Should().BeEquivalentTo(new[] { "A1", "B1", "C1" });
    }

    [Fact]
    public void ExtractDependencies_AbsoluteRef()
    {
        var deps = FormulaDependencyExtractor.ExtractCellRefs("=$A$1+$B$1");
        deps.Should().BeEquivalentTo(new[] { "$A$1", "$B$1" });
    }

    [Fact]
    public void Sheet_SetCellFormula_ComputesResult()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 10 };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 20 };
        sheet.SetCellFormula(2, 0, "=A1+A2"); // A3

        sheet.Cells["A3"].Value.Should().Be(30.0);
    }

    [Fact]
    public void Sheet_RecalculateDependents_UpdatesDependentCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 10 };
        sheet.SetCellFormula(1, 0, "=A1+5"); // A2

        sheet.Cells["A2"].Value.Should().Be(15.0);

        sheet.Cells["A1"].Value = 20;
        sheet.RecalculateDependents("A1");

        sheet.Cells["A2"].Value.Should().Be(25.0);
    }

    [Fact]
    public void Sheet_ChainDependencies()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 5 };
        sheet.SetCellFormula(1, 0, "=A1*2");  // A2
        sheet.SetCellFormula(2, 0, "=A2+3");  // A3

        sheet.Cells["A2"].Value.Should().Be(10.0);
        sheet.Cells["A3"].Value.Should().Be(13.0);

        sheet.Cells["A1"].Value = 10;
        sheet.RecalculateDependents("A1");

        sheet.Cells["A2"].Value.Should().Be(20.0);
        sheet.Cells["A3"].Value.Should().Be(23.0);
    }
}
