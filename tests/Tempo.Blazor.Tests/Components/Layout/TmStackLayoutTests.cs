using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Layout;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Layout;

public class TmStackLayoutTests : LocalizationTestBase
{
    [Fact]
    public void TmStackLayout_Horizontal_Renders_Children_Row()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.Orientation, StackOrientation.Horizontal)
            .AddChildContent("<span>Item 1</span><span>Item 2</span>"));

        var stack = cut.Find(".tm-stack-layout");
        stack.ClassList.Contains("tm-stack-layout--horizontal").Should().BeTrue();
    }

    [Fact]
    public void TmStackLayout_Vertical_Renders_Children_Column()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.Orientation, StackOrientation.Vertical)
            .AddChildContent("<span>Item 1</span><span>Item 2</span>"));

        cut.Find(".tm-stack-layout--vertical").Should().NotBeNull();
    }

    [Fact]
    public void TmStackLayout_Spacing_Applies_Gap()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.Spacing, 4));

        var stack = cut.Find(".tm-stack-layout");
        stack.GetAttribute("style").Should().Contain("gap");
    }

    [Fact]
    public void TmStackLayout_AlignItems_Applies_CSS()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.AlignItems, AlignItems.Center));

        var stack = cut.Find(".tm-stack-layout");
        stack.GetAttribute("style").Should().Contain("align-items: center");
    }

    [Fact]
    public void TmStackLayout_JustifyContent_Applies_CSS()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.JustifyContent, JustifyContent.SpaceBetween));

        var stack = cut.Find(".tm-stack-layout");
        stack.GetAttribute("style").Should().Contain("justify-content: space-between");
    }

    [Fact]
    public void TmStackLayout_Wrap_Applies_FlexWrap()
    {
        var cut = Render<TmStackLayout>(p => p
            .Add(x => x.Wrap, true));

        var stack = cut.Find(".tm-stack-layout");
        stack.GetAttribute("style").Should().Contain("flex-wrap: wrap");
    }
}
