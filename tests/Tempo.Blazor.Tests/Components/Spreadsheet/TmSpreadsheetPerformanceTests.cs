using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetPerformanceTests : LocalizationTestBase
{
    [Fact]
    public void Render_NoFreezeRows_RendersVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var virtualize = cut.FindComponents<Virtualize<int>>();
        virtualize.Count.Should().Be(1);
    }

    [Fact]
    public void Render_WithFreezeRows_DoesNotRenderVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5, FreezeRowCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var virtualize = cut.FindComponents<Virtualize<int>>();
        virtualize.Count.Should().Be(0);
    }

    [Fact]
    public void Render_DuringEditing_DoesNotRenderVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Start editing via double-click on first cell
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // After editing starts, Virtualize should be removed
        var virtualize = cut.FindComponents<Virtualize<int>>();
        virtualize.Count.Should().Be(0);
    }

    [Fact]
    public void Render_LargeSheet_RendersOnlyVisibleRows()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1000, ColumnCount = 10 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // With Virtualize, not all 1000 rows should be rendered in the DOM
        var rows = cut.FindAll(".tm-spreadsheet-row");
        rows.Count.Should().BeLessThan(1000);
    }

    [Fact]
    public void FocusAsync_Exists_AsPublicMethod()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var method = typeof(TmSpreadsheetGrid).GetMethod("FocusAsync");
        method.Should().NotBeNull();
        method!.IsPublic.Should().BeTrue();
        method.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public void DisplayValueCache_ReturnsCachedValue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        sheet.Cells["A1"] = new SpreadsheetCell
        {
            Value = 1234.567,
            Style = new SpreadsheetCellStyle { NumberFormat = "#,##0.00" },
            DisplayValue = "1,234.57"
        };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cell = cut.Find(".tm-spreadsheet-cell");
        cell.TextContent.Should().Contain("1,234.57");
    }

    [Fact]
    public void Root_FocusGridAsync_Exists()
    {
        var cut = RenderComponent<TmSpreadsheet>();

        var method = typeof(TmSpreadsheet).GetMethod("FocusGridAsync");
        method.Should().NotBeNull();
        method!.IsPublic.Should().BeTrue();
        method.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public void Render_FreezeRowsThenRemove_ReenablesVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5, FreezeRowCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        cut.FindComponents<Virtualize<int>>().Count.Should().Be(0);

        sheet.FreezeRowCount = 0;
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Sheet, sheet));

        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);
    }

    [Fact]
    public void Render_EditThenCancel_ReenablesVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Virtualize is present initially
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);

        // Start editing
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // Virtualize removed during edit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(0);

        // Cancel edit via Escape on the input element
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Virtualize restored after cancel
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);
    }

    [Fact]
    public void Render_EditThenCommit_ReenablesVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Start editing
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // Virtualize removed during edit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(0);

        // Commit edit via Enter on the input element
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Virtualize restored after commit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);
    }
}
