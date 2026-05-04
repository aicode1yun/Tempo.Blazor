using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmColorPaletteTests : LocalizationTestBase
{
    [Fact]
    public void TmColorPalette_Renders_Grid_Of_Swatches()
    {
        var cut = RenderComponent<TmColorPalette>();

        var swatches = cut.FindAll(".tm-color-palette-swatch");
        swatches.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TmColorPalette_Custom_Colors_Uses_Provided()
    {
        var colors = new[] { "#FF0000", "#00FF00", "#0000FF" };
        var cut = RenderComponent<TmColorPalette>(p => p
            .Add(c => c.Colors, colors));

        var swatches = cut.FindAll(".tm-color-palette-swatch");
        swatches.Count.Should().Be(3);
    }

    [Fact]
    public void TmColorPalette_Click_Swatch_Fires_ValueChanged()
    {
        string? selected = null;
        var colors = new[] { "#FF0000", "#00FF00" };
        var cut = RenderComponent<TmColorPalette>(p => p
            .Add(c => c.Colors, colors)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => selected = v)));

        var swatches = cut.FindAll(".tm-color-palette-swatch");
        swatches[1].Click();

        selected.Should().Be("#00FF00");
    }

    [Fact]
    public void TmColorPalette_Selected_Swatch_Has_Selected_Class()
    {
        var colors = new[] { "#FF0000", "#00FF00" };
        var cut = RenderComponent<TmColorPalette>(p => p
            .Add(c => c.Colors, colors)
            .Add(c => c.Value, "#00FF00"));

        var swatches = cut.FindAll(".tm-color-palette-swatch");
        swatches[1].ClassList.Should().Contain("tm-color-palette-swatch--selected");
    }

    [Fact]
    public void TmColorPalette_Clear_Button_Clears_Value()
    {
        string? changed = null;
        var cut = RenderComponent<TmColorPalette>(p => p
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        var clearBtn = cut.Find(".tm-color-palette-clear");
        clearBtn.Click();

        changed.Should().Be(string.Empty);
    }

    [Fact]
    public void TmColorPalette_HideClear_Hides_Button()
    {
        var cut = RenderComponent<TmColorPalette>(p => p
            .Add(c => c.ShowClearButton, false));

        cut.FindAll(".tm-color-palette-clear").Should().BeEmpty();
    }
}
