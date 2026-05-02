using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetAutoFillHandleTests : LocalizationTestBase
{
    [Fact]
    public void AutoFillHandle_RenderedOnActiveCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "A1" };
        sheet.ActiveCellRef = "A1";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var handle = cut.Find(".tm-spreadsheet-autofill-handle");
        handle.Should().NotBeNull();
    }

    [Fact]
    public void AutoFillHandle_NotRenderedOnNonActiveCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "A1" };
        sheet.ActiveCellRef = "A1";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var b1 = cells.First(c => c.GetAttribute("title") == "B1");
        b1.QuerySelector(".tm-spreadsheet-autofill-handle").Should().BeNull();
    }

    [Fact]
    public void AutoFillHandle_RenderedOnSelectionEndCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "A1" };
        sheet.Cells["B2"] = new SpreadsheetCell { Value = "B2" };
        sheet.ActiveCellRef = "A1";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Simulate range selection by invoking SelectRow or using Shift+click
        // For this test we simulate via Shift+click on B2
        var b2 = cut.FindAll(".tm-spreadsheet-cell").First(c => c.GetAttribute("title") == "B2");
        b2.Click(new MouseEventArgs { ShiftKey = true });

        cut.Render();

        var handle = cut.Find(".tm-spreadsheet-autofill-handle");
        var parentCell = handle?.ParentElement;
        parentCell?.GetAttribute("title").Should().Be("B2");
    }
}
