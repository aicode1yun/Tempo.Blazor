using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

public class TmGaugeTests : LocalizationTestBase
{
    [Fact]
    public void ArcGauge_RendersArcPath()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Arc)
            .Add(x => x.Value, 65));

        cut.Find("svg").Should().NotBeNull();
        cut.Find(".tm-gauge").Should().NotBeNull();
        cut.FindAll("path.tm-gauge__track").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CircularGauge_RendersCirclePath()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Circular)
            .Add(x => x.Value, 65));

        cut.FindAll("circle.tm-gauge__track").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LinearGauge_RendersRectBar()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Linear)
            .Add(x => x.Value, 65));

        cut.FindAll("rect.tm-gauge__track").Count.Should().BeGreaterThan(0);
        cut.FindAll("rect.tm-gauge__fill").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Gauge_Ranges_Rendered()
    {
        var ranges = new List<GaugeRange>
        {
            new(0, 50, "#22c55e"),
            new(50, 80, "#f59e0b"),
            new(80, 100, "#ef4444")
        };

        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Linear)
            .Add(x => x.Value, 65)
            .Add(x => x.Ranges, ranges));

        cut.FindAll("rect.tm-gauge__range").Count.Should().Be(3);
    }

    [Fact]
    public void Gauge_ValueLabel_Displayed()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Circular)
            .Add(x => x.Value, 65)
            .Add(x => x.ShowValue, true));

        cut.Markup.Should().Contain("65");
    }

    [Fact]
    public void Gauge_LabelFormat_Applied()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Circular)
            .Add(x => x.Value, 65)
            .Add(x => x.ShowValue, true)
            .Add(x => x.LabelFormat, "{0}%"));

        cut.Markup.Should().Contain("65%");
    }

    [Fact]
    public void Gauge_ValueWithinMinMax()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Linear)
            .Add(x => x.Value, 150)
            .Add(x => x.Min, 0)
            .Add(x => x.Max, 100));

        // Fill should not exceed 100%
        var fill = cut.Find("rect.tm-gauge__fill");
        var width = fill.GetAttribute("width") ?? "0";
        var track = cut.Find("rect.tm-gauge__track");
        var trackWidth = track.GetAttribute("width") ?? "0";
        double.Parse(width).Should().BeLessThanOrEqualTo(double.Parse(trackWidth));
    }

    [Fact]
    public void CustomClass_Applied()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Arc)
            .Add(x => x.Value, 50)
            .Add(x => x.Class, "my-gauge"));

        cut.Find(".tm-gauge").ClassList.Should().Contain("my-gauge");
    }

    [Fact]
    public void Animated_HasAnimatedClass()
    {
        var cut = RenderComponent<TmGauge>(p => p
            .Add(x => x.Type, GaugeType.Arc)
            .Add(x => x.Value, 50)
            .Add(x => x.Animated, true));

        cut.Find(".tm-gauge").ClassList.Should().Contain("tm-gauge--animated");
    }
}
