using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetKeyboardTests : LocalizationTestBase
{
    // ── Grid-level shortcut events ──

    [Fact]
    public void Grid_CtrlZ_FiresUndoRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnUndoRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlY_FiresRedoRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnRedoRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "y", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlB_FiresBoldToggleRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnBoldToggleRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "b", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlI_FiresItalicToggleRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnItalicToggleRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "i", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlU_FiresUnderlineToggleRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnUnderlineToggleRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "u", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlA_FiresSelectAllRequested()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        bool fired = false;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet)
            .Add(p => p.OnSelectAllRequested, EventCallback.Factory.Create(this, () => fired = true)));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "a", CtrlKey = true });

        fired.Should().BeTrue();
    }

    [Fact]
    public void Grid_CtrlHome_MovesToA1()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        sheet.ActiveCellRef = "D4";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "Home", CtrlKey = true });

        sheet.ActiveCellRef.Should().Be("A1");
    }

    [Fact]
    public void Grid_CtrlEnd_MovesToLastUsedCell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 10, ColumnCount = 10 };
        sheet.SetCellValue(3, 3, "Last"); // D4

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "End", CtrlKey = true });

        sheet.ActiveCellRef.Should().Be("D4");
    }

    [Fact]
    public void Grid_Home_MovesToFirstColumn()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        sheet.ActiveCellRef = "D3";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "Home" });

        sheet.ActiveCellRef.Should().Be("A3");
    }

    [Fact]
    public void Grid_End_MovesToLastColumn()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 5 };
        sheet.ActiveCellRef = "B3";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "End" });

        sheet.ActiveCellRef.Should().Be("E3");
    }

    // ── ARIA attributes ──

    [Fact]
    public void Grid_HasRoleGrid()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.GetAttribute("role").Should().Be("grid");
    }

    [Fact]
    public void Grid_HasAriaRowcountAndColcount()
    {
        var sheet = new SpreadsheetSheet { RowCount = 10, ColumnCount = 20 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.GetAttribute("aria-rowcount").Should().Be("10");
        grid.GetAttribute("aria-colcount").Should().Be("20");
    }

    [Fact]
    public void Grid_CellsHaveRoleGridcell()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        foreach (var cell in cells)
        {
            cell.GetAttribute("role").Should().Be("gridcell");
        }
    }

    [Fact]
    public void Grid_SelectedCell_HasAriaSelectedTrue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2, ActiveCellRef = "A1" };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        cells[0].GetAttribute("aria-selected").Should().Be("true"); // A1 is active
    }

    [Fact]
    public void Grid_ColumnHeadersHaveRoleColumnheader()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var headers = cut.FindAll(".tm-spreadsheet-col-headers .tm-spreadsheet-header-cell");
        foreach (var header in headers)
        {
            header.GetAttribute("role").Should().Be("columnheader");
        }
    }

    [Fact]
    public void Grid_RowsHaveRoleRow()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var rows = cut.FindAll(".tm-spreadsheet-row");
        foreach (var row in rows)
        {
            row.GetAttribute("role").Should().Be("row");
        }
    }

    // ── Integration: TmSpreadsheet shortcuts ──

    [Fact]
    public void Spreadsheet_CtrlB_TogglesBold()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "Test");
        sheet.ActiveCellRef = "A1";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "b", CtrlKey = true });

        sheet.GetOrCreateCell("A1").Style.Bold.Should().BeTrue();
    }

    [Fact]
    public void Spreadsheet_CtrlI_TogglesItalic()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "Test");
        sheet.ActiveCellRef = "A1";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "i", CtrlKey = true });

        sheet.GetOrCreateCell("A1").Style.Italic.Should().BeTrue();
    }

    [Fact]
    public void Spreadsheet_CtrlU_TogglesUnderline()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "Test");
        sheet.ActiveCellRef = "A1";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "u", CtrlKey = true });

        sheet.GetOrCreateCell("A1").Style.Underline.Should().BeTrue();
    }

    [Fact]
    public void Spreadsheet_CtrlZ_UndoesLastCommand()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(0, 0, "Test");
        sheet.ActiveCellRef = "A1";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "b", CtrlKey = true }); // bold
        sheet.GetOrCreateCell("A1").Style.Bold.Should().BeTrue();

        grid.KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true }); // undo

        sheet.GetOrCreateCell("A1").Style.Bold.Should().BeFalse();
    }

    [Fact]
    public void Spreadsheet_CtrlA_SelectsAllCells()
    {
        // Use a small sheet so virtualization renders all rows
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RowsCount, 5)
            .Add(p => p.ColumnsCount, 5));
        var sheet = cut.Instance.Workbook.ActiveSheet!;

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "a", CtrlKey = true });

        var selectedCells = cut.FindAll(".tm-spreadsheet-cell--selected");
        selectedCells.Count.Should().Be(sheet.RowCount * sheet.ColumnCount);
    }

    [Fact]
    public void Spreadsheet_CtrlHome_MovesToA1()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.ActiveCellRef = "D10";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "Home", CtrlKey = true });

        sheet.ActiveCellRef.Should().Be("A1");
    }

    [Fact]
    public void Spreadsheet_CtrlEnd_MovesToLastUsedCell()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var sheet = cut.Instance.Workbook.ActiveSheet!;
        sheet.SetCellValue(5, 5, "Last"); // F6
        sheet.ActiveCellRef = "A1";

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "End", CtrlKey = true });

        sheet.ActiveCellRef.Should().Be("F6");
    }

    // ── Formula bar focus trap ──

    [Fact]
    public void FormulaBar_Tab_CommitsAndFiresTabPressed()
    {
        bool tabPressed = false;
        string? committedValue = null;

        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Initial")
            .Add(p => p.IsEditing, true)
            .Add(p => p.OnValueCommitted, EventCallback.Factory.Create<string?>(this, v => committedValue = v))
            .Add(p => p.OnTabPressed, EventCallback.Factory.Create(this, () => tabPressed = true)));

        var input = cut.Find(".tm-spreadsheet-formula-bar__input");
        input.Input("NewValue");
        input.KeyDown(new KeyboardEventArgs { Key = "Tab" });

        committedValue.Should().Be("NewValue");
        tabPressed.Should().BeTrue();
    }

    [Fact]
    public void CanvasGrid_ArrowRight_MovesActiveCell()
    {
        var cut = RenderComponent<TmSpreadsheet>(parameters => parameters
            .Add(p => p.RenderMode, SpreadsheetRenderMode.Canvas)
            .Add(p => p.RowsCount, 5)
            .Add(p => p.ColumnsCount, 5));
        var sheet = cut.Instance.Workbook.ActiveSheet!;

        cut.Find(".tm-spreadsheet-canvas-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        sheet.ActiveCellRef.Should().Be("B1");
    }
}
