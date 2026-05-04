using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmFlatColorPickerTests : LocalizationTestBase
{
    [Fact]
    public void TmFlatColorPicker_Renders_Gradient_And_Palette()
    {
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "#FF0000"));

        cut.Find(".tm-color-gradient").Should().NotBeNull();
        cut.Find(".tm-color-palette").Should().NotBeNull();
    }

    [Fact]
    public void TmFlatColorPicker_HidePalette_Hides_Palette()
    {
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.ShowPalette, false));

        cut.FindAll(".tm-color-palette").Should().BeEmpty();
    }

    [Fact]
    public void TmFlatColorPicker_HidePreview_Hides_Preview()
    {
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.ShowPreview, false));

        cut.FindAll(".tm-flat-color-picker-preview-row").Should().BeEmpty();
    }

    [Fact]
    public void TmFlatColorPicker_ValueChanged_Fires_When_Gradient_Changes()
    {
        string? changed = null;
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "#000000")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        // Change via gradient input
        var input = cut.Find("input.tm-color-gradient-input");
        input.Change("255");

        changed.Should().NotBeNull();
    }

    [Fact]
    public void TmFlatColorPicker_Palette_Click_Fires_ValueChanged()
    {
        string? changed = null;
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "#000000")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        var swatch = cut.Find(".tm-color-palette-swatch");
        swatch.Click();

        changed.Should().NotBeNull();
    }

    [Fact]
    public void TmFlatColorPicker_Format_Rgba_Shows_Rgba()
    {
        var cut = RenderComponent<TmFlatColorPicker>(p => p
            .Add(c => c.Value, "rgba(10, 20, 30, 0.5)")
            .Add(c => c.Format, ColorFormat.Rgba));

        var valueText = cut.Find(".tm-flat-color-picker-value-text").TextContent;
        valueText.Should().Contain("rgba");
    }
}
