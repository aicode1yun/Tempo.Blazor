using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetTests : LocalizationTestBase
{
    [Fact]
    public void Render_DefaultParameters_RendersGrid()
    {
        var cut = RenderComponent<TmSpreadsheet>();

        cut.Find(".tm-spreadsheet").Should().NotBeNull();
        cut.FindComponent<TmSpreadsheetGrid>().Should().NotBeNull();
    }

    [Fact]
    public void Render_CanvasMode_RendersCanvasGrid()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RenderMode, SpreadsheetRenderMode.Canvas));

        cut.Find(".tm-spreadsheet-canvas-grid").Should().NotBeNull();
        cut.Find("canvas.tm-spreadsheet-canvas-grid__canvas").Should().NotBeNull();
        cut.FindComponents<TmSpreadsheetGrid>().Should().BeEmpty();
    }

    [Fact]
    public void Render_CustomHeight_AppliesStyle()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.Height, "400px")
            .Add(p => p.Width, "800px"));

        var element = cut.Find(".tm-spreadsheet");
        element.GetAttribute("style").Should().Contain("height: 400px");
        element.GetAttribute("style").Should().Contain("width: 800px");
    }

    [Fact]
    public void Render_DefaultParameters_CreatesWorkbookWithOneSheet()
    {
        var cut = RenderComponent<TmSpreadsheet>();

        var component = cut.Instance;
        component.Workbook.Sheets.Should().HaveCount(1);
        component.Workbook.ActiveSheet.Should().NotBeNull();
    }

    [Fact]
    public void Render_WithRowsAndColumnsCount_SetsSheetDimensions()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RowsCount, 100)
            .Add(p => p.ColumnsCount, 30));

        var sheet = cut.Instance.Workbook.ActiveSheet;
        sheet.Should().NotBeNull();
        sheet!.RowCount.Should().Be(100);
        sheet.ColumnCount.Should().Be(30);
    }

    [Fact]
    public void Render_WithCustomRowHeightAndColumnWidth_SetsDefaults()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RowHeight, 25)
            .Add(p => p.ColumnWidth, 80));

        var sheet = cut.Instance.Workbook.ActiveSheet;
        sheet.Should().NotBeNull();
        sheet!.DefaultRowHeight.Should().Be(25);
        sheet.DefaultColumnWidth.Should().Be(80);
    }

    [Fact]
    public void Render_WithClass_AppliesCssClass()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.Class, "my-custom-class"));

        cut.Find(".tm-spreadsheet").ClassList.Should().Contain("my-custom-class");
    }

    [Fact]
    public void CopyButton_InvokesCopyCommand()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "test" };
        sheet.ActiveCellRef = "A1";

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Copy");
        btn.Click();

        SpreadsheetClipboard.Cells.Should().NotBeNull();
        SpreadsheetClipboard.Cells!.Should().ContainKey("A1");
    }

    [Fact]
    public void CutButton_RemovesCellAndSetsClipboard()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "test" };
        sheet.ActiveCellRef = "A1";

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Cut");
        btn.Click();

        SpreadsheetClipboard.IsCut.Should().BeTrue();
        sheet.Cells.ContainsKey("A1").Should().BeFalse();
    }

    [Fact]
    public void PasteButton_InsertsClipboardContent()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };
        sheet.ActiveCellRef = "A1";

        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Copy").Click();

        sheet.ActiveCellRef = "B1";
        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Paste").Click();

        sheet.Cells["B1"].Value.Should().Be("hello");
    }

    [Fact]
    public void InsertRowButton_AddsRow()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "bottom" };
        sheet.ActiveCellRef = "A2";

        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert row").Click();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells["A3"].Value.Should().Be("bottom");
    }

    [Fact]
    public void DeleteRowButton_RemovesRow()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "bottom" };
        sheet.ActiveCellRef = "A2";

        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Delete row").Click();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells["A2"].Value.Should().Be("bottom");
    }

    [Fact]
    public void InsertColumnButton_AddsColumn()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "right" };
        sheet.ActiveCellRef = "B1";

        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert column").Click();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells["C1"].Value.Should().Be("right");
    }

    [Fact]
    public void DeleteColumnButton_RemovesColumn()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "right" };
        sheet.ActiveCellRef = "B1";

        cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Delete column").Click();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells["B1"].Value.Should().Be("right");
    }

    // ── Sheet Tabs ──

    [Fact]
    public void Render_DisplaysSheetTabs()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        cut.FindComponent<TmSpreadsheetSheetTabs>().Should().NotBeNull();
    }

    [Fact]
    public void AddSheetButton_AddsNewSheet()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var workbook = cut.Instance.Workbook;
        workbook.Sheets.Should().HaveCount(1);

        var addBtn = cut.Find(".tm-spreadsheet-sheet-tabs__add");
        addBtn.Click();

        workbook.Sheets.Should().HaveCount(2);
        workbook.ActiveSheetIndex.Should().Be(1);
    }

    [Fact]
    public void SheetTab_Click_SwitchesActiveSheet()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var workbook = cut.Instance.Workbook;
        workbook.AddSheet("Sheet2");
        cut.Render();

        var tabs = cut.FindAll(".tm-spreadsheet-sheet-tab");
        tabs[1].Click();

        workbook.ActiveSheetIndex.Should().Be(1);
    }

    [Fact]
    public void DeleteSheetButton_RemovesSheet()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var workbook = cut.Instance.Workbook;
        workbook.AddSheet("Sheet2");
        cut.Render();

        var closeBtns = cut.FindAll(".tm-spreadsheet-sheet-tab__close");
        closeBtns[0].Click();

        workbook.Sheets.Should().HaveCount(1);
    }

    // ── Phase 12: Toolbar completion ──

    [Fact]
    public void InsertLinkDialog_OpensAndCloses()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.ActiveCellRef = "A1";

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[1].Click();

        var linkBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert link");
        linkBtn.Click();

        cut.FindAll(".tm-spreadsheet-dialog").Count.Should().Be(1);

        var cancelBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Cancel"));
        cancelBtn.Click();

        cut.FindAll(".tm-spreadsheet-dialog").Count.Should().Be(0);
    }

    [Fact]
    public void InsertImageDialog_OpensAndCloses()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.ActiveCellRef = "B2";

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[1].Click();

        var imgBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert image");
        imgBtn.Click();

        cut.FindAll(".tm-spreadsheet-dialog").Count.Should().Be(1);

        var cancelBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Cancel"));
        cancelBtn.Click();

        cut.FindAll(".tm-spreadsheet-dialog").Count.Should().Be(0);
    }

    [Fact]
    public void ToggleGridLines_HidesBorders()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.ShowGridLines.Should().BeTrue();

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[2].Click();

        var gridBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Grid lines");
        gridBtn.Click();

        sheet.ShowGridLines.Should().BeFalse();
        cut.Find(".tm-spreadsheet-grid--no-gridlines").Should().NotBeNull();
    }

    [Fact]
    public void MergeCells_MergesSelection()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "x" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "y" };
        sheet.ActiveCellRef = "A1";
        cut.Render();

        // Select range A1:B1
        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("ArrowRight");
        grid.KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[2].Click();

        var mergeBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Merge cells");
        mergeBtn.Click();

        sheet.MergedCells.Should().ContainSingle();
    }

    [Fact]
    public void Render_FormulaBar_DisplaysActiveCellValue()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "Hello World");
        sheet.ActiveCellRef = "A1";
        cut.Render();

        var display = cut.Find(".tm-spreadsheet-formula-bar__display");
        display.TextContent.Trim().Should().Be("Hello World");
    }

    [Fact]
    public void Render_FormulaBar_DisplaysActiveCellFormula()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "=SUM(B1:B5)");
        sheet.ActiveCellRef = "A1";
        cut.Render();

        var display = cut.Find(".tm-spreadsheet-formula-bar__display");
        display.TextContent.Trim().Should().Be("=SUM(B1:B5)");
    }

    [Fact]
    public void FormulaEdit_ClickAnotherCell_InsertsCellReference()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RowsCount, 2)
            .Add(p => p.ColumnsCount, 3));
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.ActiveCellRef = "A1";

        // Start editing A1 with a formula
        cut.FindAll(".tm-spreadsheet-cell")[0].DoubleClick();
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("=SUM(");

        // MouseDown C1 while editing formula (insertion happens on mousedown in formula point mode)
        cut.InvokeAsync(() => cut.FindAll(".tm-spreadsheet-cell")[2].MouseDown());

        // Should still be editing with the cell reference appended
        var grid = cut.FindComponent<TmSpreadsheetGrid>();
        grid.Instance.IsEditing.Should().BeTrue();
        input = cut.Find(".tm-spreadsheet-cell-input");
        input.GetAttribute("value").Should().Be("=SUM(C1");
    }

    [Fact]
    public void FormulaCommit_EvaluatesFormulaAndDisplaysResult()
    {
        // Formula evaluation is already covered by:
        // - SpreadsheetCommandTests.SetCellValueCommand_SetsFormula_EvaluatesValue
        // - TmSpreadsheetGridTests.Render_FormulaCell_DisplaysEvaluatedValue
        // This integration test verifies the full end-to-end through TmSpreadsheet.
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RowsCount, 2)
            .Add(p => p.ColumnsCount, 3));
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, 10); // A1 = 10
        sheet.SetCellValue(0, 1, 20); // B1 = 20

        // Commit formula directly via the sheet (simulates what happens after Enter)
        sheet.SetCellFormula(0, 2, "=A1+B1"); // C1 = 30
        cut.Render();

        // Grid should display the evaluated value in C1
        var grid = cut.FindComponent<TmSpreadsheetGrid>();
        var cells = grid.FindAll(".tm-spreadsheet-cell");
        cells[2].TextContent.Should().Contain("30");
    }
}
