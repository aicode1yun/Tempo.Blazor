using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Layout;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Layout;

public class TmSplitterTests : LocalizationTestBase
{
    [Fact]
    public void TmSplitter_Renders_Panes()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1.AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        cut.FindAll(".tm-splitter__pane").Count.Should().Be(2);
    }

    [Fact]
    public void TmSplitter_Vertical_Has_Vertical_Class()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .Add(x => x.Orientation, SplitterOrientation.Vertical)
            .AddChildContent<TmSplitterPane>(pane1 => pane1.AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        cut.Find(".tm-splitter--vertical").Should().NotBeNull();
    }

    [Fact]
    public void TmSplitter_Resizer_Bar_Renders()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1.AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        cut.FindAll(".tm-splitter__resizer").Count.Should().Be(1);
    }

    [Fact]
    public void TmSplitter_Pane_Collapsible_Renders_Collapse_Button()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Collapsible, true)
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        cut.Find(".tm-splitter__collapse-btn").Should().NotBeNull();
    }

    [Fact]
    public void TmSplitter_Pane_Size_Applies_Style()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Size, "200px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.GetAttribute("style").Should().Contain("flex-basis: 200px");
    }

    [Fact]
    public void TmSplitter_Pane_MinMaxSize_Applies_Style()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.MinSize, "100px")
                .Add(x => x.MaxSize, "400px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        var style = pane.GetAttribute("style");
        style.Should().Contain("min-width: 100px");
        style.Should().Contain("max-width: 400px");
    }

    [Fact]
    public void TmSplitter_Pane_Collapsed_Applies_Collapsed_Class()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Collapsed, true)
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.ClassList.Contains("tm-splitter__pane--collapsed").Should().BeTrue();
    }

    [Fact]
    public void TmSplitter_Collapse_Button_Toggles_Collapsed()
    {
        bool collapsed = false;
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Collapsible, true)
                .Add(x => x.Collapsed, collapsed)
                .Add(x => x.CollapsedChanged, EventCallback.Factory.Create<bool>(this, v => collapsed = v))
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var btn = cut.Find(".tm-splitter__collapse-btn");
        btn.Click();

        collapsed.Should().BeTrue();
    }

    [Fact]
    public void TmSplitter_Last_Pane_Has_No_Resizer()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1.AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2"))
            .AddChildContent<TmSplitterPane>(pane3 => pane3.AddChildContent("Pane 3")));

        cut.FindAll(".tm-splitter__resizer").Count.Should().Be(2);
    }

    [Fact]
    public void TmSplitter_Drag_Horizontal_Updates_Pane_Size()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Size, "200px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var resizer = cut.Find(".tm-splitter__resizer");
        resizer.TriggerEvent("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100, ClientY = 50 });
        resizer.TriggerEvent("onpointermove", new PointerEventArgs { ClientX = 150, ClientY = 50 });

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.GetAttribute("style").Should().Contain("flex: 0 0 250px");
    }

    [Fact]
    public void TmSplitter_Drag_Vertical_Updates_Pane_Size()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .Add(x => x.Orientation, SplitterOrientation.Vertical)
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Size, "200px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var resizer = cut.Find(".tm-splitter__resizer");
        resizer.TriggerEvent("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 50, ClientY = 100 });
        resizer.TriggerEvent("onpointermove", new PointerEventArgs { ClientX = 50, ClientY = 130 });

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.GetAttribute("style").Should().Contain("flex: 0 0 230px");
    }

    [Fact]
    public void TmSplitter_Drag_Left_Decreases_Pane_Size()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Size, "200px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var resizer = cut.Find(".tm-splitter__resizer");
        resizer.TriggerEvent("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 100, ClientY = 50 });
        resizer.TriggerEvent("onpointermove", new PointerEventArgs { ClientX = 60, ClientY = 50 });

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.GetAttribute("style").Should().Contain("flex: 0 0 160px");
    }

    [Fact]
    public void TmSplitter_Drag_NonLeftButton_Ignored()
    {
        var cut = RenderComponent<TmSplitter>(p => p
            .AddChildContent<TmSplitterPane>(pane1 => pane1
                .Add(x => x.Size, "200px")
                .AddChildContent("Pane 1"))
            .AddChildContent<TmSplitterPane>(pane2 => pane2.AddChildContent("Pane 2")));

        var resizer = cut.Find(".tm-splitter__resizer");
        resizer.TriggerEvent("onpointerdown", new PointerEventArgs { Button = 1, ClientX = 100, ClientY = 50 });
        resizer.TriggerEvent("onpointermove", new PointerEventArgs { ClientX = 150, ClientY = 50 });

        var pane = cut.FindAll(".tm-splitter__pane")[0];
        pane.GetAttribute("style").Should().Contain("flex-basis: 200px");
    }
}
