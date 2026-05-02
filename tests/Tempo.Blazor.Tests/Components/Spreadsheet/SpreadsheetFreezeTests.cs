using Bunit;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFreezeTests : LocalizationTestBase
{
    [Fact]
    public void FreezeRow_FirstRowHeaderHasStickyStyle()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3, FreezeRowCount = 2 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var rowHeaders = cut.FindAll(".tm-spreadsheet-row-header");
        var firstRow = rowHeaders[0];
        firstRow.GetAttribute("style").Should().Contain("position: sticky");
        firstRow.GetAttribute("style").Should().Contain("top:");
    }

    [Fact]
    public void FreezeRow_NonFrozenRowHeaderHasNoStickyStyle()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3, FreezeRowCount = 2 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var rowHeaders = cut.FindAll(".tm-spreadsheet-row-header");
        var thirdRow = rowHeaders[2];
        thirdRow.GetAttribute("style").Should().NotContain("position: sticky");
    }

    [Fact]
    public void FreezeCol_FirstCellHasStickyLeftStyle()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 5, FreezeColumnCount = 2 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Frozen" };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var a1 = cut.FindAll(".tm-spreadsheet-cell").First(c => c.GetAttribute("title") == "A1");
        a1.GetAttribute("style").Should().Contain("position: sticky");
        a1.GetAttribute("style").Should().Contain("left:");
    }

    [Fact]
    public void FreezeCol_NonFrozenCellHasNoStickyLeftStyle()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 5, FreezeColumnCount = 2 };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "Free" };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var c1 = cut.FindAll(".tm-spreadsheet-cell").First(c => c.GetAttribute("title") == "C1");
        c1.GetAttribute("style").Should().NotContain("position: sticky");
    }

    [Fact]
    public void FreezeBoth_FirstCellHasStickyTopAndLeft()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5, FreezeRowCount = 1, FreezeColumnCount = 1 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Corner" };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var a1 = cut.FindAll(".tm-spreadsheet-cell").First(c => c.GetAttribute("title") == "A1");
        var style = a1.GetAttribute("style");
        style.Should().Contain("position: sticky");
        style.Should().Contain("top:");
        style.Should().Contain("left:");
        style.Should().Contain("z-index: 3");
    }

    [Fact]
    public void FreezeBoth_ColumnHeaderHasStickyLeftAndZIndex()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 5, FreezeRowCount = 1, FreezeColumnCount = 2 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var colHeaders = cut.FindAll(".tm-spreadsheet-header-cell.tm-spreadsheet-header-cell");
        // First col header is corner, second is A, third is B, etc.
        var colA = colHeaders[1];
        var style = colA.GetAttribute("style");
        style.Should().Contain("position: sticky");
        style.Should().Contain("left:");
        style.Should().Contain("z-index: 3");
    }
}
