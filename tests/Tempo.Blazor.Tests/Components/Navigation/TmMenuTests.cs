using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Navigation;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Navigation;

public class TmMenuTests : LocalizationTestBase
{
    [Fact]
    public void TmMenu_Renders_Items()
    {
        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Items, new[]
            {
                new MenuItem { Text = "Home", Icon = "home" },
                new MenuItem { Text = "About", Icon = "info" }
            }));

        cut.FindAll(".tm-menu__item").Count.Should().Be(2);
    }

    [Fact]
    public void TmMenu_Horizontal_Has_Horizontal_Class()
    {
        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Orientation, MenuOrientation.Horizontal));

        cut.Find(".tm-menu--horizontal").Should().NotBeNull();
    }

    [Fact]
    public void TmMenu_Click_Fires_OnItemClick()
    {
        MenuItem? clicked = null;
        var items = new[] { new MenuItem { Text = "Home" } };

        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.OnItemClick, EventCallback.Factory.Create<MenuItem>(this, i => clicked = i)));

        cut.Find(".tm-menu__link").Click();
        clicked?.Text.Should().Be("Home");
    }

    [Fact]
    public void TmMenu_Disabled_Item_Does_Not_Fire_Click()
    {
        MenuItem? clicked = null;
        var items = new[] { new MenuItem { Text = "Home", Disabled = true } };

        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.OnItemClick, EventCallback.Factory.Create<MenuItem>(this, i => clicked = i)));

        cut.Find(".tm-menu__item--disabled").Should().NotBeNull();
    }

    [Fact]
    public void TmMenu_Item_With_Href_Renders_Anchor()
    {
        var items = new[] { new MenuItem { Text = "Home", Href = "/home" } };

        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Items, items));

        cut.Find("a.tm-menu__link").GetAttribute("href").Should().Be("/home");
    }

    [Fact]
    public void TmMenu_Separator_Renders_Separator()
    {
        var items = new[]
        {
            new MenuItem { Text = "Home" },
            new MenuItem { IsSeparator = true },
            new MenuItem { Text = "About" }
        };

        var cut = RenderComponent<TmMenu>(p => p
            .Add(x => x.Items, items));

        cut.FindAll(".tm-menu__separator").Count.Should().Be(1);
    }
}
