using Bunit;
using System.Collections;
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
    public void Render_DuringEditing_KeepsVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Start editing via double-click on first cell
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // Editing should not force the grid back to rendering every row.
        var virtualize = cut.FindComponents<Virtualize<int>>();
        virtualize.Count.Should().Be(1);
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
    public void Render_LargeSheet_RendersFewerCellsThanTotalSheet()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1000, ColumnCount = 50 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells.Count.Should().BeLessThan(sheet.RowCount * sheet.ColumnCount);
    }

    [Fact]
    public void Render_BuildsColumnLetterCache()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 30 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cache = GetPrivateField<string[]>(cut.Instance, "_columnLetters");

        cache.Should().HaveCount(30);
        cache[26].Should().Be("AA");
    }

    [Fact]
    public void Render_BuildsGeometryPrefixCaches()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Rows[1] = new SpreadsheetRow { Index = 1, Height = 40 };
        sheet.Columns[1] = new SpreadsheetColumn { Index = 1, Width = 120 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.RowHeight, 20)
            .Add(p => p.ColumnWidth, 64));

        var rowOffsets = GetPrivateField<double[]>(cut.Instance, "_rowOffsets");
        var columnOffsets = GetPrivateField<double[]>(cut.Instance, "_columnOffsets");

        rowOffsets.Should().Equal(0, 20, 60, 80);
        columnOffsets.Should().Equal(0, 64, 184, 248);
    }

    [Fact]
    public void Render_BuildsMergedCellLookup()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1));

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var startLookup = GetPrivateField<IDictionary>(cut.Instance, "_mergedStartLookup");
        var hiddenLookup = GetPrivateField<object>(cut.Instance, "_mergedHiddenLookup");
        var hiddenCount = (int)hiddenLookup.GetType().GetProperty("Count")!.GetValue(hiddenLookup)!;

        startLookup.Count.Should().Be(1);
        hiddenCount.Should().Be(3);
    }

    [Fact]
    public void Render_WithStyledCell_PopulatesStyleCache()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.GetOrCreateCell("A1").Style.Bold = true;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cache = GetPrivateField<IDictionary>(cut.Instance, "_cellStyleCache");

        cache.Count.Should().BeGreaterThan(0);
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
    public void Render_EditThenCancel_KeepsVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Virtualize is present initially
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);

        // Start editing
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // Virtualize remains active during edit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);

        // Cancel edit via Escape on the input element
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Virtualize remains active after cancel
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);
    }

    [Fact]
    public void Render_EditThenCommit_KeepsVirtualize()
    {
        var sheet = new SpreadsheetSheet { RowCount = 50, ColumnCount = 5 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Start editing
        var firstCell = cut.Find(".tm-spreadsheet-cell");
        firstCell.DoubleClick();

        // Virtualize remains active during edit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);

        // Commit edit via Enter on the input element
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Virtualize remains active after commit
        cut.FindComponents<Virtualize<int>>().Count.Should().Be(1);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (T)field!.GetValue(instance)!;
    }
}
