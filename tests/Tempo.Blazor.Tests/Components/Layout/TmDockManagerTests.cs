using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Layout;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Layout;

/// <summary>TDD tests for TmDockManager.</summary>
public class TmDockManagerTests : LocalizationTestBase
{
    // ── DM-7: render několika panelů ────────────────────────────

    [Fact]
    public void TmDockManager_Renders_Multiple_Panes()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "pane1")
                .Add(x => x.Title, "Explorer")
                .Add(x => x.Position, DockPosition.Left)
                .AddChildContent("Explorer content"))
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "pane2")
                .Add(x => x.Title, "Editor")
                .Add(x => x.Position, DockPosition.Center)
                .AddChildContent("Editor content")));

        cut.Render(); // re-render after child panes register themselves

        // Ensure panes were registered
        cut.Instance.Panes.Count.Should().Be(2);
        cut.Instance.Panes[0].Position.Should().Be(DockPosition.Left, "pane1 position");
        cut.Instance.Panes[0].IsVisible.Should().BeTrue("pane1 visible");
        cut.Instance.Panes[1].Position.Should().Be(DockPosition.Center, "pane2 position");

        var markup = cut.Markup;
        markup.Should().Contain("tm-dock-area--left");
        cut.Find(".tm-dock-area--left").TextContent.Should().Contain("Explorer");
        cut.Find(".tm-dock-area--center").TextContent.Should().Contain("Editor");
    }

    // ── DM-8: drag panelu na edge dockuje ───────────────────────

    [Fact]
    public void TmDockManager_Drag_Pane_To_Right_Docks_It()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p1")
                .Add(x => x.Title, "Properties")
                .Add(x => x.Position, DockPosition.Left)
                .AddChildContent("Props")));

        cut.Render();

        var paneHeader = cut.Find(".tm-dock-pane-header");
        paneHeader.DragStart();

        // Drop overlay should appear
        cut.Find(".tm-dock-drop-overlay").Should().NotBeNull();

        // Drop on right zone
        var rightZone = cut.Find(".tm-dock-zone--right");
        rightZone.Drop();

        cut.Find(".tm-dock-area--right").TextContent.Should().Contain("Properties");
    }

    // ── DM-9: undock vytvoří floating window ────────────────────

    [Fact]
    public void TmDockManager_Float_Button_Creates_Floating_Window()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p1")
                .Add(x => x.Title, "Terminal")
                .Add(x => x.Position, DockPosition.Bottom)
                .Add(x => x.CanFloat, true)
                .AddChildContent("Terminal content")));

        cut.Render();

        var floatBtn = cut.Find(".tm-dock-tab-float");
        floatBtn.Click();

        cut.Find(".tm-dock-floating").Should().NotBeNull();
        cut.Find(".tm-dock-floating").TextContent.Should().Contain("Terminal");
    }

    // ── DM-10: close tlačítko odstraní panel ────────────────────

    [Fact]
    public void TmDockManager_Close_Button_Removes_Pane()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p1")
                .Add(x => x.Title, "Output")
                .Add(x => x.Position, DockPosition.Center)
                .Add(x => x.CanClose, true)
                .AddChildContent("Output content")));

        cut.Render();

        var closeBtn = cut.Find(".tm-dock-tab-close");
        closeBtn.Click();

        cut.FindAll(".tm-dock-area--center .tm-dock-pane, .tm-dock-area--center .tm-dock-tab-group")
            .Should().BeEmpty();
    }

    // ── DM-11: tabs když je více panelů v jedné oblasti ─────────

    [Fact]
    public void TmDockManager_Multiple_Center_Panes_Show_Tabs()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p1")
                .Add(x => x.Title, "File A")
                .Add(x => x.Position, DockPosition.Center)
                .Add(x => x.IsActive, true)
                .AddChildContent("Content A"))
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p2")
                .Add(x => x.Title, "File B")
                .Add(x => x.Position, DockPosition.Center)
                .AddChildContent("Content B")));

        cut.Render();

        var tabs = cut.FindAll(".tm-dock-tab");
        tabs.Count.Should().Be(2);
        tabs[0].TextContent.Should().Contain("File A");
        tabs[1].TextContent.Should().Contain("File B");

        // Active tab content rendered
        cut.Find(".tm-dock-tab-content").TextContent.Should().Contain("Content A");
    }

    [Fact]
    public void TmDockManager_Tab_Click_Switches_Active_Pane()
    {
        var cut = RenderComponent<TmDockManager>(p => p
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p1")
                .Add(x => x.Title, "Alpha")
                .Add(x => x.Position, DockPosition.Center)
                .Add(x => x.IsActive, true)
                .AddChildContent("Alpha content"))
            .AddChildContent<TmDockPane>(pane => pane
                .Add(x => x.Id, "p2")
                .Add(x => x.Title, "Beta")
                .Add(x => x.Position, DockPosition.Center)
                .AddChildContent("Beta content")));

        cut.Render();

        var tabs = cut.FindAll(".tm-dock-tab");
        tabs[1].Click();

        cut.Find(".tm-dock-tab-content").TextContent.Should().Contain("Beta content");
        cut.FindAll(".tm-dock-tab")[1].ClassList.Should().Contain("tm-dock-tab--active");
    }
}
