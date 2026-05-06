using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetGridTests : LocalizationTestBase
{
    [Fact]
    public void Render_NullSheet_ShowsEmptyMessage()
    {
        var cut = RenderComponent<TmSpreadsheetGrid>();

        cut.Find(".tm-spreadsheet-grid--empty").TextContent.Should().Contain("No sheet available");
    }

    [Fact]
    public void Render_WithSheet_RendersHeaders()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var colHeaders = cut.FindAll(".tm-spreadsheet-col-headers .tm-spreadsheet-header-cell");
        colHeaders.Count.Should().Be(3);
        colHeaders[0].TextContent.Trim().Should().Be("A");
        colHeaders[1].TextContent.Trim().Should().Be("B");
        colHeaders[2].TextContent.Trim().Should().Be("C");
    }

    [Fact]
    public void Render_WithSheet_RendersRowHeaders()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var rowHeaders = cut.FindAll(".tm-spreadsheet-row-header");
        rowHeaders.Count.Should().Be(3);
        rowHeaders[0].TextContent.Trim().Should().Be("1");
        rowHeaders[1].TextContent.Trim().Should().Be("2");
        rowHeaders[2].TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public void Render_WithSheet_RendersCorrectNumberOfCells()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 4 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells.Count.Should().Be(12); // 3 rows × 4 cols
    }

    [Fact]
    public void Render_WithCellValue_DisplaysValue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.SetCellValue(1, 1, "Hello World");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var b2Cell = cells[3]; // row 2, col 2 (0-based index = 3)
        b2Cell.TextContent.Should().Contain("Hello World");
    }

    [Fact]
    public void Render_WithCellDisplayValue_PrioritizesDisplayValue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1, ColumnCount = 1 };
        var cell = sheet.GetOrCreateCell("A1");
        cell.Value = 123.456;
        cell.DisplayValue = "$123.46";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].TextContent.Should().Contain("$123.46");
    }

    [Fact]
    public void Render_WithCustomRowHeight_AppliesHeight()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 1 };
        sheet.Rows[1] = new SpreadsheetRow { Height = 40 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.RowHeight, 20));

        var rowHeaders = cut.FindAll(".tm-spreadsheet-row-header");
        rowHeaders[0].GetAttribute("style").Should().Contain("height: 20px");
        rowHeaders[1].GetAttribute("style").Should().Contain("height: 40px");
    }

    [Fact]
    public void Render_WithCustomColumnWidth_AppliesWidth()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1, ColumnCount = 2 };
        sheet.Columns[1] = new SpreadsheetColumn { Width = 120 };

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.ColumnWidth, 64));

        var colHeaders = cut.FindAll(".tm-spreadsheet-col-headers .tm-spreadsheet-header-cell");
        colHeaders.Count.Should().Be(2);
        colHeaders[0].GetAttribute("style").Should().Contain("width: 64px");
        colHeaders[1].GetAttribute("style").Should().Contain("width: 120px");
    }

    [Fact]
    public void Render_CellHasTitleAttribute()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1, ColumnCount = 1 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cell = cut.Find(".tm-spreadsheet-cell");
        cell.GetAttribute("title").Should().Be("A1");
    }

    [Fact]
    public void Render_ColumnHeadersBeyondZ_Correct()
    {
        var sheet = new SpreadsheetSheet { RowCount = 1, ColumnCount = 30 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var colHeaders = cut.FindAll(".tm-spreadsheet-col-headers .tm-spreadsheet-header-cell");
        colHeaders[25].TextContent.Trim().Should().Be("Z");
        colHeaders[26].TextContent.Trim().Should().Be("AA");
        colHeaders[27].TextContent.Trim().Should().Be("AB");
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 2: Selection & Editing
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Click_Cell_ActivatesCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[4].Click(); // B2 (index 4 in 3-col grid: 0=A1,1=B1,2=C1,3=A2,4=B2)

        sheet.ActiveCellRef.Should().Be("B2");
        cut.Instance.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void ShiftArrow_ExtendsSelection()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("ArrowRight");
        grid.KeyDown(new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });

        sheet.ActiveCellRef.Should().Be("B2");
        cut.Instance.HasRangeSelection.Should().BeTrue();

        var selectedCells = cut.FindAll(".tm-spreadsheet-cell--selected");
        selectedCells.Count.Should().Be(2); // B1:B2
    }

    [Fact]
    public void DoubleClick_Cell_StartsEditing()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.SetCellValue(0, 0, "Initial");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick();

        cut.Instance.IsEditing.Should().BeTrue();
        sheet.ActiveCellRef.Should().Be("A1");
        cut.Find(".tm-spreadsheet-cell-input").Should().NotBeNull();
    }

    [Fact]
    public void Enter_DuringEditing_FiresCommitEvent()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        (string CellRef, string? Value)? received = null;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.CellValueCommitted, EventCallback.Factory.Create<(string, string?)>(this, v => received = v)));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick();

        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("New Value");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.Instance.IsEditing.Should().BeFalse();
        received.Should().NotBeNull();
        received!.Value.CellRef.Should().Be("A1");
        received!.Value.Value.Should().Be("New Value");
    }

    [Fact]
    public void Escape_DuringEditing_CancelsEdit()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.SetCellValue(0, 0, "Original");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick();

        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("Changed");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.Instance.IsEditing.Should().BeFalse();
        sheet.GetOrCreateCell("A1").Value.Should().Be("Original");
    }

    [Fact]
    public void ArrowDown_MovesActiveCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("ArrowDown");

        sheet.ActiveCellRef.Should().Be("A2");
    }

    [Fact]
    public void ArrowRight_MovesActiveCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("ArrowRight");

        sheet.ActiveCellRef.Should().Be("B1");
    }

    [Fact]
    public void Tab_MovesActiveCellHorizontally()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("Tab");

        sheet.ActiveCellRef.Should().Be("B1");
    }

    [Fact]
    public void F2_StartsEditing()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.Focus();
        grid.KeyDown(new KeyboardEventArgs { Key = "F2" });

        cut.Instance.IsEditing.Should().BeTrue();
    }

    [Fact]
    public void Click_RowHeader_SelectsEntireRow()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var rowHeaders = cut.FindAll(".tm-spreadsheet-row-header");
        rowHeaders[1].Click(); // Row 2

        sheet.ActiveCellRef.Should().Be("A2");
        cut.Instance.HasRangeSelection.Should().BeTrue();
        var selectedCells = cut.FindAll(".tm-spreadsheet-cell--selected");
        selectedCells.Count.Should().Be(3); // A2:C2
    }

    [Fact]
    public void Click_ColumnHeader_SelectsEntireColumn()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var colHeaders = cut.FindAll(".tm-spreadsheet-col-headers .tm-spreadsheet-header-cell");
        colHeaders[1].Click(); // Column B

        sheet.ActiveCellRef.Should().Be("B1");
        cut.Instance.HasRangeSelection.Should().BeTrue();
        var selectedCells = cut.FindAll(".tm-spreadsheet-cell--selected");
        selectedCells.Count.Should().Be(3); // B1:B3
    }

    [Fact]
    public void Click_Corner_SelectsAllCells()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        cut.Find(".tm-spreadsheet-corner").Click();

        sheet.ActiveCellRef.Should().Be("A1");
        cut.Instance.HasRangeSelection.Should().BeTrue();
        var selectedCells = cut.FindAll(".tm-spreadsheet-cell--selected");
        selectedCells.Count.Should().Be(9); // 3×3
    }

    [Fact]
    public void ActiveCell_HasOutlineClass()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[1].Click(); // B1

        var active = cut.Find(".tm-spreadsheet-cell--active");
        active.GetAttribute("title").Should().Be("B1");
    }

    [Fact]
    public void EnterInEdit_MovesDown()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick(); // edit A1
        cut.Find(".tm-spreadsheet-cell-input").KeyDown("Enter");

        sheet.ActiveCellRef.Should().Be("A2");
    }

    [Fact]
    public void TabInEdit_MovesRight()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick(); // edit A1
        cut.Find(".tm-spreadsheet-cell-input").KeyDown("Tab");

        sheet.ActiveCellRef.Should().Be("B1");
    }

    [Fact]
    public void ShiftTabInEdit_MovesLeft()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.ActiveCellRef = "B1";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[1].DoubleClick(); // edit B1
        cut.Find(".tm-spreadsheet-cell-input").KeyDown(new KeyboardEventArgs { Key = "Tab", ShiftKey = true });

        sheet.ActiveCellRef.Should().Be("A1");
    }

    [Fact]
    public void FormulaEntry_FiresCommitEvent()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        (string CellRef, string? Value)? received = null;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.CellValueCommitted, EventCallback.Factory.Create<(string, string?)>(this, v => received = v)));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick();

        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("=SUM(A1:A10)");
        input.KeyDown("Enter");

        received.Should().NotBeNull();
        received!.Value.CellRef.Should().Be("A1");
        received!.Value.Value.Should().Be("=SUM(A1:A10)");
    }

    // ── Auto-edit on printable key ──

    [Fact]
    public void PrintableKey_StartsEditWithCharacter()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "x" });

        cut.Instance.IsEditing.Should().BeTrue();
        cut.Find(".tm-spreadsheet-cell-input").GetAttribute("value").Should().Be("x");
    }

    [Fact]
    public void PrintableKey_WithExistingCellValue_Overrides()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.SetCellValue(0, 0, "Old");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "N" });

        cut.Instance.IsEditing.Should().BeTrue();
        cut.Find(".tm-spreadsheet-cell-input").GetAttribute("value").Should().Be("N");
    }

    // ── Formula evaluation display ──

    [Fact]
    public void Render_FormulaCell_DisplaysEvaluatedValue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 3 };
        sheet.SetCellValue(0, 0, 10); // A1 = 10
        sheet.SetCellValue(0, 1, 20); // B1 = 20
        sheet.SetCellFormula(0, 2, "=A1+B1"); // C1 = 30

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[2].TextContent.Should().Contain("30");
    }

    [Fact]
    public void Render_FormulaCell_DisplaysErrorForInvalidFormula()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        sheet.SetCellFormula(0, 0, "=INVALID(");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].TextContent.Should().Contain("#ERROR");
    }

    // ── Cell reference insertion during formula edit ──

    [Fact]
    public void Click_AnotherCell_DuringTextEdit_CommitsEdit()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 3 };
        sheet.ActiveCellRef = "A1";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        // Start editing A1 with plain text
        cut.FindAll(".tm-spreadsheet-cell")[0].DoubleClick();
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("Hello");

        // Click C1 while editing text — must re-query after re-render
        cut.FindAll(".tm-spreadsheet-cell")[2].Click();

        // Should commit and move active cell
        cut.Instance.IsEditing.Should().BeFalse();
        sheet.ActiveCellRef.Should().Be("C1");
    }

    [Fact]
    public void ActiveCellChanged_EventFires()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        string? receivedRef = null;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.ActiveCellChanged, EventCallback.Factory.Create<string?>(this, r => receivedRef = r)));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[1].Click();

        receivedRef.Should().Be("B1");
    }

    [Fact]
    public void CellValueCommitted_EventFires()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        (string CellRef, string? Value)? received = null;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.CellValueCommitted, EventCallback.Factory.Create<(string, string?)>(this, v => received = v)));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].DoubleClick();
        var input = cut.Find(".tm-spreadsheet-cell-input");
        input.Input("Test");
        input.KeyDown("Enter");

        received.Should().NotBeNull();
        received!.Value.CellRef.Should().Be("A1");
        received!.Value.Value.Should().Be("Test");
    }

    [Fact]
    public void CtrlC_FiresCopyRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnCopyRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "c", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void CtrlV_FiresPasteRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnPasteRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "v", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void CtrlX_FiresCutRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnCutRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "x", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void DeleteKey_FiresDeleteRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnDeleteRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown("Delete");

        fired.Should().BeTrue();
    }

    [Fact]
    public void RightClick_OpensContextMenu()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.ContextMenu();

        cut.FindAll(".tm-spreadsheet-context-menu").Count.Should().Be(1);
        cut.FindAll(".tm-spreadsheet-context-menu__item").Count.Should().Be(18);
    }
}
