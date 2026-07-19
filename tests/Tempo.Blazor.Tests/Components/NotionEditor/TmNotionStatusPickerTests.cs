using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TmNotionStatusPickerTests : LocalizationTestBase
{
    public TmNotionStatusPickerTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Status_Placeholder"] = "Status label",
            ["Notion_Status_Color_Gray"] = "Gray",
            ["Notion_Status_Color_Blue"] = "Blue",
            ["Notion_Status_Color_Green"] = "Green",
            ["Notion_Status_Color_Yellow"] = "Yellow",
            ["Notion_Status_Color_Red"] = "Red",
            ["Notion_Status_Color_Purple"] = "Purple",
            ["Notion_Status_Insert"] = "Insert status"
        });
    }

    [Fact]
    public void StatusPicker_WhenHidden_RendersNothing()
    {
        var cut = Render<TmNotionStatusPicker>(p => p
            .Add(x => x.Visible, false));

        cut.FindAll(".tm-notion-status-picker").Should().BeEmpty();
    }

    [Fact]
    public void StatusPicker_RendersInitialLabelAndColor()
    {
        var cut = Render<TmNotionStatusPicker>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Top, 120)
            .Add(x => x.Left, 240)
            .Add(x => x.InitialLabel, "DONE")
            .Add(x => x.InitialColor, NotionStatusColor.Green));

        cut.Find(".tm-notion-status-picker").GetAttribute("style").Should().Contain("top:120px");
        cut.Find(".tm-notion-status").ClassList.Should().Contain("tm-notion-status--green");
        cut.Find(".tm-notion-status__label").TextContent.Should().Be("DONE");
    }

    [Fact]
    public async Task StatusPicker_SelectsColorAndInsertsTrimmedLabel()
    {
        (string Label, NotionStatusColor Color) inserted = default;
        var cut = Render<TmNotionStatusPicker>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Top, 120)
            .Add(x => x.Left, 240)
            .Add(x => x.InitialLabel, "  IN PROGRESS  ")
            .Add(x => x.OnInserted,
                EventCallback.Factory.Create<(string, NotionStatusColor)>(
                    this, args => inserted = args)));

        await cut.Find(".tm-notion-status-picker__swatch--blue").ClickAsync(new MouseEventArgs());
        await cut.Find(".tm-notion-status-picker__insert").ClickAsync(new MouseEventArgs());

        inserted.Label.Should().Be("IN PROGRESS");
        inserted.Color.Should().Be(NotionStatusColor.Blue);
    }

    [Fact]
    public async Task StatusPicker_DoesNotInsertEmptyLabel()
    {
        var fired = false;
        var cut = Render<TmNotionStatusPicker>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Top, 120)
            .Add(x => x.Left, 240)
            .Add(x => x.OnInserted,
                EventCallback.Factory.Create<(string, NotionStatusColor)>(
                    this, _ => fired = true)));

        var button = cut.Find(".tm-notion-status-picker__insert");
        button.HasAttribute("disabled").Should().BeTrue();
        await button.ClickAsync(new MouseEventArgs());

        fired.Should().BeFalse();
    }
}
