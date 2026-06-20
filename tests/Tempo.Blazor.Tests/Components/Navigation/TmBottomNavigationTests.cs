using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Navigation;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Navigation;

public class TmBottomNavigationTests : LocalizationTestBase
{
    [Fact]
    public void TmBottomNavigation_Renders_Items()
    {
        var cut = RenderComponent<TmBottomNavigation>(p => p
            .Add(x => x.Items, new[]
            {
                new BottomNavItem { Text = "Home", Icon = "home" },
                new BottomNavItem { Text = "Search", Icon = "search" },
                new BottomNavItem { Text = "Profile", Icon = "user" }
            }));

        var items = cut.FindAll(".tm-bottom-nav__item");
        items.Count.Should().Be(3);
    }

    [Fact]
    public void TmBottomNavigation_Click_Fires_OnItemClick()
    {
        BottomNavItem? clicked = null;
        var items = new[]
        {
            new BottomNavItem { Text = "Home", Icon = "home", Href = "/" },
            new BottomNavItem { Text = "Search", Icon = "search", Href = "/search" }
        };

        var cut = RenderComponent<TmBottomNavigation>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.OnItemClick, EventCallback.Factory.Create<BottomNavItem>(this, i => clicked = i)));

        cut.FindAll(".tm-bottom-nav__item")[1].QuerySelector("button, a")?.Click();
        clicked?.Text.Should().Be("Search");
    }

    [Fact]
    public void TmBottomNavigation_SelectedItem_Has_Active_Class()
    {
        var items = new[]
        {
            new BottomNavItem { Text = "Home", Icon = "home" },
            new BottomNavItem { Text = "Search", Icon = "search" }
        };

        var cut = RenderComponent<TmBottomNavigation>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.SelectedItem, items[1]));

        var navItems = cut.FindAll(".tm-bottom-nav__item");
        navItems[1].ClassList.Contains("tm-bottom-nav__item--active").Should().BeTrue();
    }

    [Fact]
    public void TmBottomNavigation_Item_With_Href_Renders_Anchor()
    {
        var items = new[]
        {
            new BottomNavItem { Text = "Home", Icon = "home", Href = "/home" }
        };

        var cut = RenderComponent<TmBottomNavigation>(p => p
            .Add(x => x.Items, items));

        cut.Find("a.tm-bottom-nav__link").Should().NotBeNull();
    }
}
