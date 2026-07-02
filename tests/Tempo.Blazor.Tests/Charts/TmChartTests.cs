using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>TDD tests for TmChart SVG component.</summary>
public class TmChartTests : LocalizationTestBase
{
    private static ChartData SimpleBarData => new()
    {
        Labels = ["Jan", "Feb", "Mar"],
        Datasets =
        [
            new ChartDataset { Label = "Sales", Values = [10, 20, 30], Color = "#3b82f6" }
        ]
    };

    private static ChartData MultiDatasetData => new()
    {
        Labels = ["Q1", "Q2", "Q3"],
        Datasets =
        [
            new ChartDataset { Label = "2024", Values = [100, 200, 150], Color = "#3b82f6" },
            new ChartDataset { Label = "2025", Values = [120, 180, 220], Color = "#ef4444" }
        ]
    };

    private static ChartData PieData => new()
    {
        Labels = ["A", "B", "C"],
        Datasets =
        [
            new ChartDataset { Label = "Share", Values = [40, 35, 25], Color = "#3b82f6" }
        ]
    };

    // ── Render basics ──

    [Fact]
    public void Chart_Renders_SvgElement()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData));

        cut.Find("svg").Should().NotBeNull();
        cut.Find(".tm-chart").Should().NotBeNull();
    }

    [Fact]
    public void Chart_WidthHeight_AppliedAsStyle()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.Width, "500px")
            .Add(x => x.Height, "400px"));

        var wrapper = cut.Find(".tm-chart");
        var style = wrapper.GetAttribute("style") ?? "";
        style.Should().Contain("width:").And.Contain("500px");
        style.Should().Contain("height:").And.Contain("400px");
    }

    [Fact]
    public void Chart_HasAriaLabel()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData));

        cut.Find("svg").GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Chart_CustomClass_Applied()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.Class, "my-chart"));

        cut.Find(".tm-chart").ClassList.Should().Contain("my-chart");
    }

    // ── Bar chart ──

    [Fact]
    public void BarChart_Renders_Rects()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData));

        // Should render rect elements for each data point
        var rects = cut.FindAll("rect.tm-chart__bar");
        rects.Count.Should().Be(3);
    }

    [Fact]
    public void BarChart_MultiDataset_GroupedBars()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, MultiDatasetData));

        // 2 datasets × 3 labels = 6 bars
        var rects = cut.FindAll("rect.tm-chart__bar");
        rects.Count.Should().Be(6);
    }

    [Fact]
    public void BarChart_ShowsLabels()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData));

        var markup = cut.Markup;
        markup.Should().Contain("Jan");
        markup.Should().Contain("Feb");
        markup.Should().Contain("Mar");
    }

    // ── Line chart ──

    [Fact]
    public void LineChart_Renders_Polyline()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Line)
            .Add(x => x.Data, SimpleBarData));

        cut.FindAll("polyline.tm-chart__line").Count.Should().Be(1);
    }

    [Fact]
    public void LineChart_Renders_DataPoints()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Line)
            .Add(x => x.Data, SimpleBarData));

        cut.FindAll("circle.tm-chart__point").Count.Should().Be(3);
    }

    // ── Pie chart ──

    [Fact]
    public void PieChart_Renders_Paths()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Pie)
            .Add(x => x.Data, PieData));

        // 3 slices
        cut.FindAll("path.tm-chart__slice").Count.Should().Be(3);
    }

    // ── Donut chart ──

    [Fact]
    public void DonutChart_Renders_Paths()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Donut)
            .Add(x => x.Data, PieData));

        cut.FindAll("path.tm-chart__slice").Count.Should().Be(3);
    }

    // ── Horizontal bar ──

    [Fact]
    public void HorizontalBarChart_Renders_Rects()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.HorizontalBar)
            .Add(x => x.Data, SimpleBarData));

        cut.FindAll("rect.tm-chart__bar").Count.Should().Be(3);
    }

    // ── Per-value colors ──

    [Fact]
    public void DonutChart_BackgroundColors_UsesCustomColorsForEachSlice()
    {
        var data = ChartDataWithBackgroundColors(["#dc2626", "#ea580c", "#16a34a"]);

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Donut)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("path.tm-chart__slice"), ["#dc2626", "#ea580c", "#16a34a"]);
    }

    [Fact]
    public void PieChart_BackgroundColors_UsesCustomColorsForEachSlice()
    {
        var data = ChartDataWithBackgroundColors(["#dc2626", "#ea580c", "#16a34a"]);

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Pie)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("path.tm-chart__slice"), ["#dc2626", "#ea580c", "#16a34a"]);
    }

    [Fact]
    public void BarChart_BackgroundColors_UsesCustomColorsForEachBar()
    {
        var data = ChartDataWithBackgroundColors(["#dc2626", "#ea580c", "#16a34a"]);

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("rect.tm-chart__bar"), ["#dc2626", "#ea580c", "#16a34a"]);
    }

    [Fact]
    public void HorizontalBarChart_BackgroundColors_UsesCustomColorsForEachBar()
    {
        var data = ChartDataWithBackgroundColors(["#dc2626", "#ea580c", "#16a34a"]);

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.HorizontalBar)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("rect.tm-chart__bar"), ["#dc2626", "#ea580c", "#16a34a"]);
    }

    [Fact]
    public void BarChart_BackgroundColors_FallsBackToDatasetColor_WhenLessColorsThanValues()
    {
        var data = ChartDataWithBackgroundColors(
            ["#dc2626", "#ea580c"],
            backgroundColor: "#0d9488",
            color: "#2563eb");

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("rect.tm-chart__bar"), ["#dc2626", "#ea580c", "#0d9488"]);
    }

    [Fact]
    public void DonutChart_BackgroundColors_FallsBackToPalette_WhenLessColorsThanValues()
    {
        var data = ChartDataWithBackgroundColors(["#dc2626", "#ea580c"]);

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Donut)
            .Add(x => x.Data, data));

        AssertFillAttributes(cut.FindAll("path.tm-chart__slice"), ["#dc2626", "#ea580c", "#10b981"]);
    }

    [Fact]
    public void Chart_NullBackgroundColors_KeepsBackwardCompatibleBehavior()
    {
        var donutCut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Donut)
            .Add(x => x.Data, PieData));
        var barCut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData));

        AssertFillAttributes(donutCut.FindAll("path.tm-chart__slice"), ["#3b82f6", "#ef4444", "#10b981"]);
        AssertFillAttributes(barCut.FindAll("rect.tm-chart__bar"), ["#3b82f6", "#3b82f6", "#3b82f6"]);
    }

    // ── Legend ──

    [Fact]
    public void Chart_ShowLegend_RendersLegend()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, MultiDatasetData)
            .Add(x => x.ShowLegend, true));

        cut.FindAll(".tm-chart__legend-item").Count.Should().Be(2);
        cut.Markup.Should().Contain("2024");
        cut.Markup.Should().Contain("2025");
    }

    [Fact]
    public void Chart_ShowLegend_BackgroundColors_UsesPerValueLegendColors()
    {
        var data = ChartDataWithBackgroundColors(
            ["#dc2626", "#ea580c", "#16a34a"],
            color: "#3b82f6");

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Donut)
            .Add(x => x.Data, data)
            .Add(x => x.ShowLegend, true));

        cut.FindAll(".tm-chart__legend-label")
            .Select(element => element.TextContent)
            .Should()
            .Equal(["A", "B", "C"]);
        AssertLegendColorStyles(cut.FindAll(".tm-chart__legend-color"), ["#dc2626", "#ea580c", "#16a34a"]);
    }

    [Fact]
    public void Chart_HideLegend_NoLegend()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.ShowLegend, false));

        cut.FindAll(".tm-chart__legend").Count.Should().Be(0);
    }

    // ── Grid ──

    [Fact]
    public void Chart_ShowGrid_RendersGridLines()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.ShowGrid, true));

        cut.FindAll("line.tm-chart__grid-line").Count.Should().BeGreaterThan(0);
    }

    // ── Values ──

    [Fact]
    public void Chart_ShowValues_DisplaysValueText()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.ShowValues, true));

        var markup = cut.Markup;
        markup.Should().Contain("10");
        markup.Should().Contain("20");
        markup.Should().Contain("30");
    }

    // ── Segment click ──

    [Fact]
    public void Chart_SegmentClick_FiresCallback()
    {
        ChartSegment? clicked = null;
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.OnSegmentClick, seg => clicked = seg));

        // Click the first bar
        cut.FindAll("rect.tm-chart__bar")[0].Click();

        clicked.Should().NotBeNull();
        clicked!.DatasetIndex.Should().Be(0);
        clicked.Index.Should().Be(0);
        clicked.Label.Should().Be("Jan");
        clicked.Value.Should().Be(10);
    }

    // ── Animated ──

    [Fact]
    public void Chart_Animated_HasAnimatedClass()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, SimpleBarData)
            .Add(x => x.Animated, true));

        cut.Find(".tm-chart").ClassList.Should().Contain("tm-chart--animated");
    }

    // ── Empty data ──

    [Fact]
    public void Chart_EmptyData_ShowsMessage()
    {
        var emptyData = new ChartData { Labels = [], Datasets = [] };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, emptyData));

        cut.Find(".tm-chart__empty").Should().NotBeNull();
    }

    private static ChartData ChartDataWithBackgroundColors(
        IReadOnlyList<string> backgroundColors,
        string? backgroundColor = null,
        string color = "#3b82f6")
    {
        return new ChartData
        {
            Labels = ["A", "B", "C"],
            Datasets =
            [
                new ChartDataset
                {
                    Label = "Outages",
                    Values = [10, 20, 30],
                    Color = color,
                    BackgroundColor = backgroundColor,
                    BackgroundColors = backgroundColors,
                }
            ]
        };
    }

    private static void AssertFillAttributes(IReadOnlyList<AngleSharp.Dom.IElement> elements, string[] expectedFills)
    {
        elements.Select(element => element.GetAttribute("fill")).Should().Equal(expectedFills);
    }

    private static void AssertLegendColorStyles(IReadOnlyList<AngleSharp.Dom.IElement> elements, string[] expectedColors)
    {
        elements.Should().HaveCount(expectedColors.Length);
        for (int i = 0; i < expectedColors.Length; i++)
        {
            elements[i].GetAttribute("style").Should().Contain($"background-color: {expectedColors[i]}");
        }
    }
}
