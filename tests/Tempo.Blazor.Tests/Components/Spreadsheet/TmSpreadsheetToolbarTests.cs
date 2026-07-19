using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetToolbarTests : LocalizationTestBase
{
    [Fact]
    public void Render_Default_DisplaysAllToolGroups()
    {
        var cut = Render<TmSpreadsheetToolbar>();

        cut.FindAll(".tm-spreadsheet-toolbar__button").Count.Should().BeGreaterThan(5);
        cut.FindAll(".tm-spreadsheet-toolbar__group").Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void UndoButton_Disabled_WhenCannotUndo()
    {
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.CanUndo, false));

        var undoBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")[0];
        undoBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void UndoButton_Enabled_WhenCanUndo()
    {
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.CanUndo, true));

        var undoBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")[0];
        undoBtn.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void BoldButton_Active_WhenIsBold()
    {
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.IsBold, true));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var boldBtn = buttons.First(b => b.InnerHtml.Contains(">B<"));
        boldBtn.ClassList.Should().Contain("tm-spreadsheet-toolbar__button--active");
    }

    [Fact]
    public void BoldButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnBoldToggle, EventCallback.Factory.Create(this, () => fired = true)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var boldBtn = buttons.First(b => b.InnerHtml.Contains(">B<"));
        boldBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ItalicButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnItalicToggle, EventCallback.Factory.Create(this, () => fired = true)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var italicBtn = buttons.First(b => b.InnerHtml.Contains(">I<"));
        italicBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void UnderlineButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnUnderlineToggle, EventCallback.Factory.Create(this, () => fired = true)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var underlineBtn = buttons.First(b => b.InnerHtml.Contains(">U<"));
        underlineBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void AlignLeftButton_Active_WhenAlignLeft()
    {
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.SelectedHorizontalAlign, "left"));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var alignBtn = buttons.First(b => b.GetAttribute("title") == "Align left");
        alignBtn.ClassList.Should().Contain("tm-spreadsheet-toolbar__button--active");
    }

    [Fact]
    public void AlignCenterButton_Click_FiresEvent()
    {
        string? align = null;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnAlignChanged, EventCallback.Factory.Create<string?>(this, v => align = v)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var centerBtn = buttons.First(b => b.GetAttribute("title") == "Align center");
        centerBtn.Click();

        align.Should().Be("center");
    }

    [Fact]
    public void UndoButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.CanUndo, true)
            .Add(p => p.OnUndo, EventCallback.Factory.Create(this, () => fired = true)));

        var undoBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")[0];
        undoBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void RedoButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.CanRedo, true)
            .Add(p => p.OnRedo, EventCallback.Factory.Create(this, () => fired = true)));

        var redoBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")[1];
        redoBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void FontFamily_Changed_FiresEvent()
    {
        string? font = null;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnFontFamilyChanged, EventCallback.Factory.Create<string?>(this, v => font = v)));

        // The TmSelect component renders a select element
        var select = cut.Find("select");
        select.Change("Segoe UI");

        font.Should().Be("Segoe UI");
    }

    [Fact]
    public void NumberFormat_Select_FiresEvent()
    {
        string? format = null;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnNumberFormatChanged, EventCallback.Factory.Create<string?>(this, v => format = v)));

        var selects = cut.FindAll("select");
        // Number format select is the third one (font, size, number format)
        var numberFormatSelect = selects[2];
        numberFormatSelect.Change("0.00");

        format.Should().Be("0.00");
    }

    [Fact]
    public void IncreaseDecimals_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnIncreaseDecimals, EventCallback.Factory.Create(this, () => fired = true)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var incBtn = buttons.First(b => b.GetAttribute("title") == "Increase decimal places");
        incBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void DecreaseDecimals_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnDecreaseDecimals, EventCallback.Factory.Create(this, () => fired = true)));

        var buttons = cut.FindAll(".tm-spreadsheet-toolbar__button");
        var decBtn = buttons.First(b => b.GetAttribute("title") == "Decrease decimal places");
        decBtn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void CopyButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnCopy, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Copy");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void CutButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnCut, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Cut");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void PasteButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnPaste, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Paste");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void InsertRowButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnInsertRow, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert row");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void DeleteRowButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnDeleteRow, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Delete row");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void InsertColumnButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnInsertColumn, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert column");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void DeleteColumnButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnDeleteColumn, EventCallback.Factory.Create(this, () => fired = true)));

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Delete column");
        btn.Click();

        fired.Should().BeTrue();
    }

    // ── Tabs ──

    [Fact]
    public void Render_DisplaysTabHeaders()
    {
        var cut = Render<TmSpreadsheetToolbar>();
        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs.Count.Should().Be(5);
        tabs[0].TextContent.Should().Contain("Home");
        tabs[1].TextContent.Should().Contain("Insert");
        tabs[2].TextContent.Should().Contain("Data");
        tabs[3].TextContent.Should().Contain("View");
        tabs[4].TextContent.Should().Contain("File");
    }

    [Fact]
    public void Click_InsertTab_SwitchesContent()
    {
        var cut = Render<TmSpreadsheetToolbar>();
        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[1].Click(); // Insert tab

        cut.Instance.ActiveTab.Should().Be("Insert");
        var insertBtn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .FirstOrDefault(b => b.GetAttribute("title") == "Insert link");
        insertBtn.Should().NotBeNull();
    }

    [Fact]
    public void InsertLinkButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnInsertLink, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[1].Click(); // Insert tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert link");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void InsertImageButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnInsertImage, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[1].Click(); // Insert tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Insert image");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void MergeCellsButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnMergeCells, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[3].Click(); // View tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Merge cells");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ToggleGridLinesButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnToggleGridLines, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[3].Click(); // View tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Grid lines");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void OpenButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnOpen, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[4].Click(); // File tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Open file");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void DownloadButton_Click_FiresEvent()
    {
        bool fired = false;
        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.OnDownload, EventCallback.Factory.Create(this, () => fired = true)));

        var tabs = cut.FindAll(".tm-spreadsheet-toolbar__tab");
        tabs[4].Click(); // File tab

        var btn = cut.FindAll(".tm-spreadsheet-toolbar__button")
            .First(b => b.GetAttribute("title") == "Download");
        btn.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void CustomTools_RenderInCorrectTab()
    {
        var customTools = new List<SpreadsheetCustomTool>
        {
            new() { IconName = "star", Title = "My Tool", Tab = "Home", Order = 0 }
        };

        var cut = Render<TmSpreadsheetToolbar>(parameters => parameters
            .Add(p => p.CustomTools, customTools));

        var groups = cut.FindAll(".tm-spreadsheet-toolbar__group");
        groups.Any(g => g.TextContent.Contains("star")).Should().BeTrue();
    }
}
