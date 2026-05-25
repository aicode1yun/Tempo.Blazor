using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;
using Xunit;

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

    [Fact]
    public void TmColorPalette_RendersRovingGridKeyboardState()
    {
        var cut = RenderPalette("#222222");

        var grid = cut.Find(".tm-color-palette-grid");
        grid.GetAttribute("role").Should().Be("grid");

        var swatches = cut.FindAll(".tm-color-palette-swatch");
        swatches.Should().HaveCount(4);
        swatches[1].GetAttribute("tabindex").Should().Be("0");
        swatches[1].GetAttribute("aria-selected").Should().Be("true");
        swatches.Where((_, index) => index != 1)
            .Should()
            .OnlyContain(swatch => swatch.GetAttribute("tabindex") == "-1");
    }

    [Fact]
    public void TmColorPalette_ArrowKeys_MoveRovingFocus()
    {
        var cut = RenderPalette("#111111");

        cut.FindAll(".tm-color-palette-swatch")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var swatchesAfterRight = cut.FindAll(".tm-color-palette-swatch");
        swatchesAfterRight[0].GetAttribute("tabindex").Should().Be("-1");
        swatchesAfterRight[1].GetAttribute("tabindex").Should().Be("0");
        swatchesAfterRight[1].ClassList.Should().Contain("tm-color-palette-swatch--keyboard-focus");

        swatchesAfterRight[1].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var swatchesAfterDown = cut.FindAll(".tm-color-palette-swatch");
        swatchesAfterDown[3].GetAttribute("tabindex").Should().Be("0");
        swatchesAfterDown[3].ClassList.Should().Contain("tm-color-palette-swatch--keyboard-focus");
    }

    [Fact]
    public void TmColorPalette_EnterAndSpace_SelectFocusedSwatch()
    {
        var selected = string.Empty;
        var cut = RenderPalette("#111111", value => selected = value);

        cut.FindAll(".tm-color-palette-swatch")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.FindAll(".tm-color-palette-swatch")[1].KeyDown(new KeyboardEventArgs { Key = "Enter" });

        selected.Should().Be("#222222");

        cut.FindAll(".tm-color-palette-swatch")[1].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.FindAll(".tm-color-palette-swatch")[2].KeyDown(new KeyboardEventArgs { Key = " " });

        selected.Should().Be("#333333");
    }

    private IRenderedComponent<TmColorPalette> RenderPalette(
        string value,
        Action<string>? onChanged = null)
        => RenderComponent<TmColorPalette>(parameters => parameters
            .Add(p => p.Value, value)
            .Add(p => p.Colors, new[] { "#111111", "#222222", "#333333", "#444444" })
            .Add(p => p.Columns, 2)
            .Add(p => p.ShowClearButton, false)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, onChanged ?? (_ => { }))));
}
