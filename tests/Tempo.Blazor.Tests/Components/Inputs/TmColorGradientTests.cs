using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmColorGradientTests : LocalizationTestBase
{
    [Fact]
    public void TmColorGradient_Renders_Area_And_Sliders()
    {
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#FF0000"));

        cut.Find(".tm-color-gradient-area").Should().NotBeNull();
        cut.Find(".tm-color-gradient-hue-track").Should().NotBeNull();
        cut.Find(".tm-color-gradient-alpha-track").Should().NotBeNull();
    }

    [Fact]
    public void TmColorGradient_HideAlpha_Hides_Alpha_Slider()
    {
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.ShowAlpha, false));

        cut.FindAll(".tm-color-gradient-alpha-track").Should().BeEmpty();
    }

    [Fact]
    public void TmColorGradient_Value_Red_Renders_Correct_Preview()
    {
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#FF0000"));

        var preview = cut.Find(".tm-color-gradient-preview");
        preview.GetAttribute("style")!.Should().Contain("rgba(255, 0, 0, 1)");
    }

    [Fact]
    public void TmColorGradient_ValueChanged_Fires_On_Input_Change()
    {
        string? changed = null;
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#000000")
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        // Simulate changing red input to 255
        var redInput = cut.Find("input.tm-color-gradient-input");
        redInput.Change("255");

        changed.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ColorFormat.Hex, "#FF0000")]
    [InlineData(ColorFormat.Rgb, "rgb(255, 0, 0)")]
    [InlineData(ColorFormat.Rgba, "rgba(255, 0, 0, 1)")]
    public void TmColorGradient_Format_Applies_To_Value(ColorFormat format, string expected)
    {
        string? changed = null;
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#FF0000")
            .Add(c => c.Format, format)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => changed = v)));

        // Trigger a change by clicking hue track
        var hueTrack = cut.Find(".tm-color-gradient-hue-track");
        hueTrack.PointerDown(new PointerEventArgs { OffsetX = 0, OffsetY = 0 });
        hueTrack.PointerUp(new PointerEventArgs { OffsetX = 0, OffsetY = 0 });

        changed.Should().StartWith(expected[..5]);
    }

    [Fact]
    public void TmColorGradient_Rgba_Input_Syncs()
    {
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "rgba(10, 20, 30, 0.5)")
            .Add(c => c.Format, ColorFormat.Rgba));

        var valueText = cut.Find(".tm-color-gradient-value-text").TextContent;
        valueText.Should().Contain("rgba(10, 20, 30, 0.5)");
    }

    [Fact]
    public void TmColorGradient_Thumb_Position_Reflects_Value()
    {
        // Pure red (saturation=1, value=1) → thumb at top-right
        var cut = Render<TmColorGradient>(p => p
            .Add(c => c.Value, "#FF0000"));

        var thumb = cut.Find(".tm-color-gradient-thumb");
        var style = thumb.GetAttribute("style")!;

        // Saturation=1 → left ~100%, Value=1 → top ~0%
        style.Should().Contain("left: 100");
        style.Should().Contain("top: 0");
    }
}
