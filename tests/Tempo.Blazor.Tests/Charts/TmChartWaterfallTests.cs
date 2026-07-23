using System.Globalization;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>TDD tests for the TmChart Waterfall type.</summary>
public class TmChartWaterfallTests : LocalizationTestBase
{
    private static ChartData ProfitBridge => new()
    {
        Labels = ["Opening", "Sales", "Costs"],
        Datasets =
        [
            new ChartDataset
            {
                Label = "Profit",
                Values = [100, 50, -30],
                Color = "#2563eb"
            }
        ]
    };

    [Fact]
    public void Waterfall_RendersCumulativeBarGeometryAndSignClasses()
    {
        var cut = Render<TmChart>(parameters => parameters
            .Add(x => x.Type, ChartType.Waterfall)
            .Add(x => x.Data, ProfitBridge)
            .Add(x => x.ShowGrid, false)
            .Add(x => x.ShowLegend, false));

        var bars = cut.FindAll("rect.tm-chart__bar");
        bars.Should().HaveCount(3);
        bars.Select(bar => bar.ClassList.ToString()).Should().Equal(
            "tm-chart__bar tm-chart__bar--positive",
            "tm-chart__bar tm-chart__bar--positive",
            "tm-chart__bar tm-chart__bar--negative");

        // Domain 0..150 maps to SVG Y 360..20.
        AssertGeometry(bars[0], 133.33, 226.67);
        AssertGeometry(bars[1], 20, 113.33);
        AssertGeometry(bars[2], 20, 68);
    }

    [Fact]
    public void Waterfall_TotalAndConnectors_AreCalculatedFromCumulativeValue()
    {
        var cut = Render<TmChart>(parameters => parameters
            .Add(x => x.Type, ChartType.Waterfall)
            .Add(x => x.Data, ProfitBridge)
            .Add(x => x.WaterfallShowTotal, true)
            .Add(x => x.WaterfallShowConnectors, true)
            .Add(x => x.ShowGrid, false)
            .Add(x => x.ShowLegend, false));

        var bars = cut.FindAll("rect.tm-chart__bar");
        bars.Should().HaveCount(4);
        bars[^1].ClassList.Should().Contain("tm-chart__bar--total");
        AssertGeometry(bars[^1], 88, 272);

        cut.FindAll("line.tm-chart__waterfall-connector").Should().HaveCount(3);
        cut.FindAll("text.tm-chart__label")
            .Select(label => label.TextContent)
            .Should().Equal(["Opening", "Sales", "Costs", "Total"]);
    }

    [Fact]
    public void Waterfall_NegativeCumulativePath_ExtendsGridBelowZero()
    {
        var data = new ChartData
        {
            Labels = ["Opening", "Loss", "Recovery"],
            Datasets =
            [
                new ChartDataset
                {
                    Label = "Cash",
                    Values = [50, -80, 20],
                    Color = "#2563eb"
                }
            ]
        };

        var cut = Render<TmChart>(parameters => parameters
            .Add(x => x.Type, ChartType.Waterfall)
            .Add(x => x.Data, data)
            .Add(x => x.ShowGrid, true)
            .Add(x => x.ShowLegend, false));

        cut.FindAll("text.tm-chart__axis-label")
            .Select(label => double.Parse(label.TextContent, CultureInfo.InvariantCulture))
            .Should().Contain(value => value < 0);

        var zeroAxis = cut.Find("line.tm-chart__axis-zero");
        Attribute(zeroAxis, "y1").Should().BeGreaterThan(20).And.BeLessThan(360);
    }

    [Fact]
    public void Waterfall_LegendAndValues_AreLocalizedAndSigned()
    {
        UseCzechLocalization();

        var cut = Render<TmChart>(parameters => parameters
            .Add(x => x.Type, ChartType.Waterfall)
            .Add(x => x.Data, ProfitBridge)
            .Add(x => x.WaterfallShowTotal, true)
            .Add(x => x.ShowValues, true)
            .Add(x => x.ShowGrid, false));

        cut.FindAll(".tm-chart__legend-label")
            .Select(label => label.TextContent)
            .Should().Equal(["Nárůst", "Pokles", "Celkem"]);
        cut.FindAll("text.tm-chart__value")
            .Select(value => value.TextContent)
            .Should().Equal(["100", "+50", "-30", "120"]);
    }

    [Fact]
    public void Waterfall_Click_ReturnsOriginalDelta()
    {
        ChartSegment? clicked = null;
        var cut = Render<TmChart>(parameters => parameters
            .Add(x => x.Type, ChartType.Waterfall)
            .Add(x => x.Data, ProfitBridge)
            .Add(x => x.ShowGrid, false)
            .Add(x => x.ShowLegend, false)
            .Add(x => x.OnSegmentClick, segment => clicked = segment));

        cut.FindAll("rect.tm-chart__bar")[2].Click();

        clicked.Should().Be(new ChartSegment(0, 2, "Costs", -30));
    }

    private static void AssertGeometry(AngleSharp.Dom.IElement element, double y, double height)
    {
        Attribute(element, "y").Should().BeApproximately(y, 0.01);
        Attribute(element, "height").Should().BeApproximately(height, 0.01);
    }

    private static double Attribute(AngleSharp.Dom.IElement element, string name)
        => double.Parse(element.GetAttribute(name)!, CultureInfo.InvariantCulture);
}
