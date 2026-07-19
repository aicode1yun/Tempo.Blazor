using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmSliderTests : LocalizationTestBase
{
    [Fact]
    public void TmSlider_Renders_Input_Range()
    {
        var cut = Render<TmSlider>();
        cut.Find("input[type='range']").Should().NotBeNull();
    }

    [Fact]
    public void TmSlider_Value_50_Renders_Correctly()
    {
        var cut = Render<TmSlider>(p => p.Add(x => x.Value, 50));
        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("50");
    }

    [Fact]
    public void TmSlider_Change_Fires_ValueChanged()
    {
        int? captured = null;
        var cut = Render<TmSlider>(p => p
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var input = cut.Find("input");
        input.Input(75);

        captured.Should().Be(75);
    }

    [Fact]
    public void TmSlider_Disabled_Has_Disabled_Attribute()
    {
        var cut = Render<TmSlider>(p => p.Add(x => x.Disabled, true));
        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TmSlider_Vertical_Has_Vertical_Class()
    {
        var cut = Render<TmSlider>(p => p.Add(x => x.Orientation, SliderOrientation.Vertical));
        cut.Find(".tm-slider--vertical").Should().NotBeNull();
    }

    [Fact]
    public void TmSlider_Step_Rounds_Value()
    {
        var cut = Render<TmSlider>(p => p
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 100)
            .Add(x => x.Step, 10));

        var input = cut.Find("input");
        input.GetAttribute("step").Should().Be("10");
    }

    [Fact]
    public void TmSlider_ShowTicks_Renders_Ticks()
    {
        var cut = Render<TmSlider>(p => p
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 10)
            .Add(x => x.ShowTicks, true));

        cut.FindAll(".tm-slider__tick").Count.Should().BeGreaterThan(0);
    }
}
