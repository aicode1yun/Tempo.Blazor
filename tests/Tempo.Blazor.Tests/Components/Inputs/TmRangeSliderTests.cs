using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmRangeSliderTests : LocalizationTestBase
{
    [Fact]
    public void TmRangeSlider_Renders_Two_Inputs()
    {
        var cut = Render<TmRangeSlider>();
        cut.FindAll("input[type='range']").Count.Should().Be(2);
    }

    [Fact]
    public void TmRangeSlider_StartValue_EndValue_Renders_Correctly()
    {
        var cut = Render<TmRangeSlider>(p => p
            .Add(x => x.StartValue, 20)
            .Add(x => x.EndValue, 80));

        var inputs = cut.FindAll("input");
        inputs[0].GetAttribute("value").Should().Be("20");
        inputs[1].GetAttribute("value").Should().Be("80");
    }

    [Fact]
    public void TmRangeSlider_StartValueChanged_Fires_Event()
    {
        int? captured = null;
        var cut = Render<TmRangeSlider>(p => p
            .Add(x => x.StartValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var inputs = cut.FindAll("input");
        inputs[0].Input(30);

        captured.Should().Be(30);
    }

    [Fact]
    public void TmRangeSlider_EndValueChanged_Fires_Event()
    {
        int? captured = null;
        var cut = Render<TmRangeSlider>(p => p
            .Add(x => x.EndValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var inputs = cut.FindAll("input");
        inputs[1].Input(70);

        captured.Should().Be(70);
    }

    [Fact]
    public void TmRangeSlider_StartValue_Constrained_By_EndValue()
    {
        int? startCaptured = null;
        var cut = Render<TmRangeSlider>(p => p
            .Add(x => x.StartValue, 50)
            .Add(x => x.EndValue, 80)
            .Add(x => x.StartValueChanged, EventCallback.Factory.Create<int?>(this, v => startCaptured = v)));

        var inputs = cut.FindAll("input");
        // Try to set start above end - should be constrained to end
        inputs[0].Input(90);

        startCaptured.Should().Be(80);
    }

    [Fact]
    public void TmRangeSlider_EndValue_Constrained_By_StartValue()
    {
        int? endCaptured = null;
        var cut = Render<TmRangeSlider>(p => p
            .Add(x => x.StartValue, 50)
            .Add(x => x.EndValue, 80)
            .Add(x => x.EndValueChanged, EventCallback.Factory.Create<int?>(this, v => endCaptured = v)));

        var inputs = cut.FindAll("input");
        // Try to set end below start - should be constrained to start
        inputs[1].Input(30);

        endCaptured.Should().Be(50);
    }

    [Fact]
    public void TmRangeSlider_Disabled_Has_Disabled_Attribute()
    {
        var cut = Render<TmRangeSlider>(p => p.Add(x => x.Disabled, true));
        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }
}
