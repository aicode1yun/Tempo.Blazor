using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Rendering;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetRenderingHelperTests
{
    [Fact]
    public void Geometry_UsesCustomAndHiddenDimensions()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Rows[1] = new SpreadsheetRow { Index = 1, Height = 40 };
        sheet.Columns[1] = new SpreadsheetColumn { Index = 1, Width = 120 };
        sheet.Columns[2] = new SpreadsheetColumn { Index = 2, IsHidden = true };
        var geometry = new SpreadsheetGridGeometry();

        geometry.Update(sheet, 20, 64);

        geometry.GetRowHeight(0).Should().Be(20);
        geometry.GetRowHeight(1).Should().Be(40);
        geometry.GetColumnWidth(1).Should().Be(120);
        geometry.GetColumnWidth(2).Should().Be(0);
        geometry.ContentWidth.Should().Be(184);
    }

    [Fact]
    public void Geometry_Zoom_ScalesDimensionsAndHitTest()
    {
        var sheet = new SpreadsheetSheet { RowCount = 10, ColumnCount = 10 };
        var geometry = new SpreadsheetGridGeometry();

        geometry.Update(sheet, 20, 64, zoom: 1.5);

        geometry.Zoom.Should().Be(1.5);
        geometry.GetRowHeight(0).Should().Be(30);
        geometry.GetColumnWidth(0).Should().Be(96);
        geometry.GetCumulativeColumnWidth(2).Should().Be(192);

        // A point at the zoomed offset for column 2 (2 * 96 + a bit) maps to column 2.
        var hit = geometry.HitTest(200, 65);
        hit.Col.Should().Be(2);
        hit.Row.Should().Be(2);
    }

    [Fact]
    public void Geometry_Zoom_PreservesHiddenAndCustomSizes()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.Rows[1] = new SpreadsheetRow { Index = 1, Height = 40 };
        sheet.Columns[2] = new SpreadsheetColumn { Index = 2, IsHidden = true };
        var geometry = new SpreadsheetGridGeometry();

        geometry.Update(sheet, 20, 64, zoom: 2.0);

        geometry.GetRowHeight(1).Should().Be(80);   // custom 40 * 2
        geometry.GetColumnWidth(2).Should().Be(0);   // hidden stays 0
        geometry.GetColumnWidth(0).Should().Be(128); // default 64 * 2
    }

    [Fact]
    public void Geometry_HitTest_MapsContentOffsetsToCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 10, ColumnCount = 10 };
        var geometry = new SpreadsheetGridGeometry();
        geometry.Update(sheet, 20, 64);

        var hit = geometry.HitTest(130, 45);

        hit.Row.Should().Be(2);
        hit.Col.Should().Be(2);
    }

    [Fact]
    public void Geometry_VisibleRanges_UseViewportAndOverscan()
    {
        var sheet = new SpreadsheetSheet { RowCount = 100, ColumnCount = 100 };
        var geometry = new SpreadsheetGridGeometry();
        geometry.Update(sheet, 20, 64);
        var viewport = new SpreadsheetViewportState(ScrollLeft: 640, ScrollTop: 200, Width: 320, Height: 100);

        geometry.GetVisibleRows(sheet, viewport, overscan: 1).Should().Be((9, 16));
        geometry.GetVisibleColumns(sheet, viewport, overscan: 2).Should().Be((8, 17));
    }

    [Fact]
    public void SelectionState_ReturnsNormalizedBoundsAndRefs()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        var selection = new SpreadsheetSelectionState
        {
            ActiveCellRef = "C3",
            SelectionStartRef = "C3",
            SelectionEndRef = "A1"
        };

        selection.GetBounds().Should().Be((0, 0, 2, 2));
        selection.GetSelectedCellRefs(sheet).Should().Contain(["A1", "B2", "C3"]);
    }
}
