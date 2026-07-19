using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

public class TmSparklineTests : LocalizationTestBase
{
    private static double[] SampleData => [10, 25, 15, 30, 20, 35, 28];

    [Fact]
    public void Render_SvgElement_Exists()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData));

        cut.Find("svg").Should().NotBeNull();
        cut.Find(".tm-sparkline").Should().NotBeNull();
    }

    [Fact]
    public void LineType_RendersPolyline()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Type, SparklineType.Line));

        cut.FindAll("polyline.tm-sparkline__line").Count.Should().Be(1);
    }

    [Fact]
    public void BarType_RendersRects()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Type, SparklineType.Bar));

        cut.FindAll("rect.tm-sparkline__bar").Count.Should().Be(7);
    }

    [Fact]
    public void AreaType_RendersPath()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Type, SparklineType.Area));

        cut.FindAll("path.tm-sparkline__area").Count.Should().Be(1);
    }

    [Fact]
    public void PieType_RendersPaths()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Type, SparklineType.Pie));

        cut.FindAll("path.tm-sparkline__slice").Count.Should().Be(7);
    }

    [Fact]
    public void Height_AppliedAsStyle()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Height, "60px"));

        var wrapper = cut.Find(".tm-sparkline");
        var style = wrapper.GetAttribute("style") ?? "";
        style.Should().Contain("height:").And.Contain("60px");
    }

    [Fact]
    public void CustomClass_Applied()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Class, "my-spark"));

        cut.Find(".tm-sparkline").ClassList.Should().Contain("my-spark");
    }

    [Fact]
    public void EmptyData_ShowsPlaceholder()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, Array.Empty<double>()));

        cut.Find(".tm-sparkline__empty").Should().NotBeNull();
    }

    [Fact]
    public void Tooltip_HasTitleWithValue()
    {
        var cut = Render<TmSparkline>(p => p
            .Add(x => x.Data, SampleData)
            .Add(x => x.Type, SparklineType.Bar));

        // Tooltip is rendered as a title element within the SVG
        var titles = cut.FindAll("title");
        titles.Count.Should().BeGreaterThan(0);
    }
}
