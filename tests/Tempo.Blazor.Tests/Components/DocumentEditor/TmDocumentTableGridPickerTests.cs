using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentTableGridPickerTests : LocalizationTestBase
{
    // ─── 8.1 Rendering ───────────────────────────────────────────────────────

    [Fact]
    public void Render_ExposesGridRole()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .GetAttribute("role").Should().Be("grid");
    }

    [Fact]
    public void Render_ExposesAriaLabel()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Render_Renders10x10Cells()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.FindAll("[role='gridcell']").Should().HaveCount(100);
    }

    [Fact]
    public void Render_CellsHaveTestIds()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-cell-0-0']").Should().NotBeNull();
        cut.Find("[data-testid='document-table-grid-cell-9-9']").Should().NotBeNull();
    }

    [Fact]
    public void Render_ShowsDimensionsDisplay()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        var dims = cut.Find(".tm-document-table-grid-picker__dims");
        dims.Should().NotBeNull();
        dims.GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void Render_InitialDimsShowTwoByTwo()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find(".tm-document-table-grid-picker__dims").TextContent
           .Should().Contain("2 x 2");
    }

    // ─── 8.2 Mouse hover ─────────────────────────────────────────────────────

    [Fact]
    public void HoverCell_HighlightsCorrectRegion()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-cell-2-3']")
           .TriggerEvent("onmouseover", new MouseEventArgs());

        var cell00 = cut.Find("[data-testid='document-table-grid-cell-0-0']");
        cell00.ClassList.Should().Contain("tm-document-table-grid-picker__cell--highlighted");

        var cell23 = cut.Find("[data-testid='document-table-grid-cell-2-3']");
        cell23.ClassList.Should().Contain("tm-document-table-grid-picker__cell--highlighted");
    }

    [Fact]
    public void HoverCell_UpdatesDimensionsLabel()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-cell-2-4']")
           .TriggerEvent("onmouseover", new MouseEventArgs());

        var dims = cut.Find(".tm-document-table-grid-picker__dims").TextContent;
        dims.Should().Contain("3").And.Contain("5");
    }

    [Fact]
    public void MouseLeave_ResetsHighlightToTwoByTwo()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-cell-4-4']")
           .TriggerEvent("onmouseover", new MouseEventArgs());

        cut.Find(".tm-document-table-grid-picker__grid")
           .TriggerEvent("onmouseleave", new MouseEventArgs());

        cut.Find(".tm-document-table-grid-picker__dims").TextContent
           .Should().Contain("2 x 2");
    }

    // ─── 8.3 Click inserts ───────────────────────────────────────────────────

    [Fact]
    public void ClickCell_InvokesOnInsertWithCorrectDimensions()
    {
        (int Rows, int Columns) result = default;
        var cut = Render<TmDocumentTableGridPicker>(parameters => parameters
            .Add(p => p.OnInsert, (Action<(int Rows, int Columns)>)(dims => result = dims)));

        cut.Find("[data-testid='document-table-grid-cell-2-3']").Click();

        result.Rows.Should().Be(3);
        result.Columns.Should().Be(4);
    }

    [Fact]
    public void ClickCell_FirstCellInserts1x1()
    {
        (int Rows, int Columns) result = default;
        var cut = Render<TmDocumentTableGridPicker>(parameters => parameters
            .Add(p => p.OnInsert, (Action<(int Rows, int Columns)>)(dims => result = dims)));

        cut.Find("[data-testid='document-table-grid-cell-0-0']").Click();

        result.Rows.Should().Be(1);
        result.Columns.Should().Be(1);
    }

    // ─── 8.4 Keyboard navigation ──────────────────────────────────────────────

    [Fact]
    public void ArrowRight_MovesKbFocus()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });

        cut.Find("[data-testid='document-table-grid-cell-1-2']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void Focus_ResetsKeyboardFocusAfterPointerHover()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-cell-4-5']")
           .TriggerEvent("onmouseover", new MouseEventArgs());
        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onfocus", new FocusEventArgs());
        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        cut.Find("[data-testid='document-table-grid-cell-2-2']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void ArrowDown_MovesKbFocusDown()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        cut.Find("[data-testid='document-table-grid-cell-2-1']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void ArrowLeft_DoesNotGoBelow0()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft" });

        cut.Find("[data-testid='document-table-grid-cell-1-0']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void ArrowUp_DoesNotGoBelow0()
    {
        var cut = Render<TmDocumentTableGridPicker>();

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });

        cut.Find("[data-testid='document-table-grid-cell-0-1']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void EnterKey_InvokesOnInsertWithKbPosition()
    {
        (int Rows, int Columns) result = default;
        var cut = Render<TmDocumentTableGridPicker>(parameters => parameters
            .Add(p => p.OnInsert, (Action<(int Rows, int Columns)>)(dims => result = dims)));

        var grid = cut.Find("[data-testid='document-table-grid-picker']");
        grid.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        grid.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        grid.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        result.Rows.Should().Be(3);
        result.Columns.Should().Be(3);
    }

    [Fact]
    public void SpaceKey_InvokesOnInsert()
    {
        (int Rows, int Columns) result = default;
        var cut = Render<TmDocumentTableGridPicker>(parameters => parameters
            .Add(p => p.OnInsert, (Action<(int Rows, int Columns)>)(dims => result = dims)));

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = " " });

        result.Rows.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EscapeKey_InvokesOnClose()
    {
        var closed = false;
        var cut = Render<TmDocumentTableGridPicker>(parameters => parameters
            .Add(p => p.OnClose, (Action)(() => closed = true)));

        cut.Find("[data-testid='document-table-grid-picker']")
           .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Escape" });

        closed.Should().BeTrue();
    }

    // ─── 8.5 Keyboard does not exceed bounds ──────────────────────────────────

    [Fact]
    public void ArrowRight_DoesNotExceedMaxColumns()
    {
        var cut = Render<TmDocumentTableGridPicker>();
        var grid = cut.Find("[data-testid='document-table-grid-picker']");

        for (int i = 0; i < 20; i++)
            grid.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });

        cut.Find("[data-testid='document-table-grid-cell-1-9']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }

    [Fact]
    public void ArrowDown_DoesNotExceedMaxRows()
    {
        var cut = Render<TmDocumentTableGridPicker>();
        var grid = cut.Find("[data-testid='document-table-grid-picker']");

        for (int i = 0; i < 20; i++)
            grid.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        cut.Find("[data-testid='document-table-grid-cell-9-1']")
           .ClassList.Should().Contain("tm-document-table-grid-picker__cell--focus");
    }
}
