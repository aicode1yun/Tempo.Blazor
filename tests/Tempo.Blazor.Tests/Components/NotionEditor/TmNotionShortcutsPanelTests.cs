using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionShortcutsPanelTests : LocalizationTestBase
{
    public TmNotionShortcutsPanelTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Shortcuts_Title"] = "Keyboard shortcuts",
            ["Notion_Shortcuts_Subtitle"] = "Fast actions.",
            ["Notion_Shortcuts_Close"] = "Close shortcuts",
            ["Shortcut_Group_Custom"] = "Custom group",
            ["Shortcut_Group_Review"] = "Review",
            ["Notion_Shortcut_CustomAction"] = "Run custom action",
            ["Notion_Shortcut_ReviewAction"] = "Open review action"
        });
    }

    [Fact]
    public void Panel_RendersGroupsFromConfigurationWithLocalizedDescriptions()
    {
        var groups = new[]
        {
            new NotionShortcutGroup
            {
                TitleKey = "Shortcut_Group_Custom",
                Items =
                [
                    new NotionShortcutItem
                    {
                        Action = "CustomAction",
                        DescriptionKey = "Notion_Shortcut_CustomAction",
                        Keys = ["Ctrl", "K"]
                    }
                ]
            },
            new NotionShortcutGroup
            {
                TitleKey = "Shortcut_Group_Review",
                Items =
                [
                    new NotionShortcutItem
                    {
                        Action = "ReviewAction",
                        DescriptionKey = "Notion_Shortcut_ReviewAction",
                        Keys = ["?"]
                    }
                ]
            }
        };

        var cut = RenderComponent<TmNotionShortcutsPanel>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Groups, groups));

        cut.FindAll(".tm-nsp__group").Should().HaveCount(2);
        cut.Find("[data-shortcut-group='Shortcut_Group_Custom']").TextContent.Should().Contain("Custom group");
        cut.Find("[data-shortcut-action='CustomAction']").TextContent.Should().Contain("Run custom action");
        cut.Find("[data-shortcut-action='CustomAction']").TextContent.Should().Contain("Ctrl");
        cut.Find("[data-shortcut-action='CustomAction']").TextContent.Should().Contain("K");
        cut.Find("[data-shortcut-action='ReviewAction']").TextContent.Should().Contain("Open review action");
        cut.Markup.Should().NotContain("Open page search");
    }

    [Fact]
    public void Panel_DoesNotRenderWhenHidden()
    {
        var cut = RenderComponent<TmNotionShortcutsPanel>(parameters => parameters
            .Add(p => p.Visible, false));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Panel_EscapeClosesThroughVisibleChanged()
    {
        var visible = true;
        var cut = RenderComponent<TmNotionShortcutsPanel>(parameters => parameters
            .Add(p => p.Visible, visible)
            .Add(p => p.VisibleChanged, EventCallback.Factory.Create<bool>(this, value => visible = value)));

        await cut.Find(".tm-nsp").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        visible.Should().BeFalse();
    }
}
