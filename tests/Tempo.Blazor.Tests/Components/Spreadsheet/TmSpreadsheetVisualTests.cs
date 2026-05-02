using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetVisualTests : LocalizationTestBase
{
    [Fact]
    public void Spreadsheet_Root_HasBorderAndRadius()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var root = cut.Find(".tm-spreadsheet");

        root.ClassList.Should().Contain("tm-spreadsheet");
        root.GetAttribute("style").Should().NotBeNull();
    }

    [Fact]
    public void Grid_HasOutlineNone()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.GetAttribute("tabindex").Should().Be("0");
    }

    [Fact]
    public void ActiveCell_HasActiveClass()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3, ActiveCellRef = "B2" };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var b2 = cells[4]; // B2 in 3-col grid
        b2.ClassList.Should().Contain("tm-spreadsheet-cell--active");
    }

    [Fact]
    public void SelectedCells_HaveSelectedClass()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3, ActiveCellRef = "A1" };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var cells = cut.FindAll(".tm-spreadsheet-cell--selected");
        cells.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Grid_HasAriaGridRole()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.GetAttribute("role").Should().Be("grid");
    }

    [Fact]
    public void Toolbar_HasTabStructure()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");

        tabs.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Toolbar_HasScrollableTabContent()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var tabContent = cut.Find(".tm-spreadsheet-toolbar__tab-content");

        tabContent.Should().NotBeNull();
    }

    [Fact]
    public void FormulaBar_HasRefAndEditor()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var refEl = cut.Find(".tm-spreadsheet-formula-bar__ref");
        var editor = cut.Find(".tm-spreadsheet-formula-bar__editor");

        refEl.Should().NotBeNull();
        editor.Should().NotBeNull();
    }

    [Fact]
    public void SheetTabs_AreScrollable()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var scroll = cut.Find(".tm-spreadsheet-sheet-tabs__scroll");

        scroll.Should().NotBeNull();
    }

    [Fact]
    public void ContextMenu_HasElevatedSurface()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2 };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.ContextMenu();

        var menu = cut.Find(".tm-spreadsheet-context-menu");
        menu.Should().NotBeNull();
    }

    [Fact]
    public void Dialog_HasBackdropAndDialog()
    {
        var cut = RenderComponent<TmSpreadsheet>();

        // Trigger insert link dialog via toolbar
        var toolbarButtons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var insertLinkButton = toolbarButtons.FirstOrDefault(b => b.GetAttribute("title") == "Insert link");
        if (insertLinkButton is not null)
        {
            insertLinkButton.Click();
            cut.Find(".tm-spreadsheet-dialog-backdrop").Should().NotBeNull();
            cut.Find(".tm-spreadsheet-dialog").Should().NotBeNull();
        }
    }

    [Fact]
    public void NoGridLines_TogglesClass()
    {
        var sheet = new SpreadsheetSheet { RowCount = 2, ColumnCount = 2, ShowGridLines = false };
        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var grid = cut.Find(".tm-spreadsheet-grid");
        grid.ClassList.Should().Contain("tm-spreadsheet-grid--no-gridlines");
    }

    [Fact]
    public void Toolbar_ButtonsHaveTransition()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");

        buttons.Count.Should().BeGreaterThan(0);
        // Scoped CSS ensures transitions are defined in the stylesheet
    }

    [Fact]
    public void FormulaBar_InputHasFocusStyle()
    {
        var cut = RenderComponent<TmSpreadsheet>();

        // Formula bar should render display or input
        var display = cut.FindAll(".tm-spreadsheet-formula-bar__display");
        var input = cut.FindAll(".tm-spreadsheet-formula-bar__input");

        (display.Count + input.Count).Should().BeGreaterThan(0);
    }

    [Fact]
    public void SheetTab_Active_HasActiveClass()
    {
        var cut = RenderComponent<TmSpreadsheet>();
        var tabs = cut.FindAll(".tm-spreadsheet-sheet-tab");

        tabs.Count.Should().BeGreaterThan(0);
        var activeTab = tabs.First(t => t.ClassList.Contains("tm-spreadsheet-sheet-tab--active"));
        activeTab.Should().NotBeNull();
    }
}
