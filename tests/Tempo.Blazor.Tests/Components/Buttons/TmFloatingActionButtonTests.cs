using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Buttons;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Buttons;

public class TmFloatingActionButtonTests : LocalizationTestBase
{
    [Fact]
    public void TmFloatingActionButton_Renders_Circular_Button()
    {
        var cut = Render<TmFloatingActionButton>();
        cut.Find(".tm-fab").Should().NotBeNull();
    }

    [Fact]
    public void TmFloatingActionButton_Click_Fires_OnClick()
    {
        bool clicked = false;
        var cut = Render<TmFloatingActionButton>(p => p
            .Add(x => x.OnClick, EventCallback.Factory.Create(this, () => clicked = true)));

        cut.Find(".tm-fab__main").Click();
        clicked.Should().BeTrue();
    }

    [Fact]
    public void TmFloatingActionButton_Position_BottomRight_Has_Class()
    {
        var cut = Render<TmFloatingActionButton>(p => p
            .Add(x => x.Position, FabPosition.BottomRight));

        cut.Find(".tm-fab--bottomright").Should().NotBeNull();
    }

    [Fact]
    public void TmFloatingActionButton_With_Items_Shows_SpeedDial()
    {
        var items = new[]
        {
            new FabItem { Icon = "edit", Label = "Edit" },
            new FabItem { Icon = "delete", Label = "Delete" }
        };

        var cut = Render<TmFloatingActionButton>(p => p
            .Add(x => x.Items, items));

        cut.Find(".tm-fab__main").Click();
        cut.FindAll(".tm-fab__item").Count.Should().Be(2);
    }

    [Fact]
    public void TmFloatingActionButton_SpeedDial_Item_Click_Fires_OnItemClick()
    {
        FabItem? clicked = null;
        var items = new[]
        {
            new FabItem { Icon = "edit", Label = "Edit" }
        };

        var cut = Render<TmFloatingActionButton>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.OnItemClick, EventCallback.Factory.Create<FabItem>(this, i => clicked = i)));

        cut.Find(".tm-fab__main").Click();
        cut.Find(".tm-fab__item").Click();

        clicked?.Label.Should().Be("Edit");
    }
}
