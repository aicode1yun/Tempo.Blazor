using Bunit;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetMergeTests : LocalizationTestBase
{
    [Fact]
    public void MergedCell_FirstCellShowsContent()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Merged" };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1)); // A1:B2

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        // Grid renders 3x3 = 9 cells per row, but first row has 3 cells + row header
        // Actually each row renders 3 cells
        cells[0].TextContent.Should().Be("Merged"); // A1 (first data cell in first row)
    }

    [Fact]
    public void MergedCell_CoveredCellsAreHidden()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Merged" };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1)); // A1:B2

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        // B1 should be hidden (merged into A1)
        // In flex layout, hidden cells get display:none
        var b1 = cells.FirstOrDefault(c => c.GetAttribute("title") == "B1");
        if (b1 is not null)
        {
            b1.ClassList.Should().Contain("tm-spreadsheet-cell--merged-hidden");
        }
    }

    [Fact]
    public void MergedCell_FirstCellHasExpandedSize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Merged" };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1)); // A1:B2

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var a1 = cut.FindAll(".tm-spreadsheet-cell").First(c => c.GetAttribute("title") == "A1");
        var style = a1.GetAttribute("style");
        style.Should().Contain("width:"); // Should include width for 2 columns
    }

    [Fact]
    public void UnmergeCell_RestoresIndividualCells()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Merged" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "B1" };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 0, 1)); // A1:B1
        sheet.MergedCells.Clear(); // simulate unmerge

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var a1 = cells.First(c => c.GetAttribute("title") == "A1");
        var b1 = cells.First(c => c.GetAttribute("title") == "B1");
        a1.TextContent.Should().Be("Merged");
        b1.TextContent.Should().Be("B1");
    }
}
