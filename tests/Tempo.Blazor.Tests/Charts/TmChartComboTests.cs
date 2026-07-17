using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>
/// TDD tests for combo charts: a Bar chart whose datasets can opt into a Line overlay
/// through <see cref="ChartDataset.RenderAs"/> (bars = periodic flows, lines = cumulative
/// values in one plot, shared Y scale).
/// </summary>
public class TmChartComboTests : LocalizationTestBase
{
    private static ChartData ComboData => new()
    {
        Labels = ["Jan", "Feb", "Mar", "Apr"],
        Datasets =
        [
            new ChartDataset { Label = "Income", Values = [10, 20, 30, 25], Color = "#22c55e" },
            new ChartDataset { Label = "Costs", Values = [8, 12, 18, 16], Color = "#ef4444" },
            new ChartDataset { Label = "Cashflow", Values = [2, 8, 12, 9], Color = "#3b82f6", RenderAs = ChartDatasetRenderAs.Line },
        ]
    };

    [Fact]
    public void ComboChart_Renders_Bars_Only_For_Bar_Datasets()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, ComboData));

        // 2 bar datasets × 4 labels = 8 rects; the Line dataset must not add bars.
        cut.FindAll("rect.tm-chart__bar").Count.Should().Be(8);
    }

    [Fact]
    public void ComboChart_Renders_Line_Overlay_With_Points()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, ComboData));

        var lines = cut.FindAll("polyline.tm-chart__line");
        lines.Count.Should().Be(1);
        lines[0].GetAttribute("points")!.Split(' ').Length.Should().Be(4);
        cut.FindAll("circle.tm-chart__point").Count.Should().Be(4);
    }

    [Fact]
    public void ComboChart_Line_Overlay_Uses_Shared_Y_Scale()
    {
        // Overlay value equal to the global max must sit at the very top of the plot area.
        var data = new ChartData
        {
            Labels = ["A", "B"],
            Datasets =
            [
                new ChartDataset { Label = "Bars", Values = [10, 20], Color = "#ef4444" },
                new ChartDataset { Label = "Line", Values = [30, 15], Color = "#3b82f6", RenderAs = ChartDatasetRenderAs.Line },
            ]
        };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        var points = cut.FindAll("circle.tm-chart__point");
        var yTop = double.Parse(points[0].GetAttribute("cy")!, System.Globalization.CultureInfo.InvariantCulture);
        var yHalf = double.Parse(points[1].GetAttribute("cy")!, System.Globalization.CultureInfo.InvariantCulture);
        yTop.Should().BeLessThan(yHalf);
        yTop.Should().BeApproximately(20, 0.5); // PT (top padding) = value == max
    }

    [Fact]
    public void ComboChart_Legend_Contains_All_Datasets()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, ComboData));

        var legend = cut.FindAll(".tm-chart__legend-item");
        legend.Count.Should().Be(3);
    }

    [Fact]
    public void ComboChart_Overlay_Point_Click_Reports_Original_Dataset_Index()
    {
        ChartSegment? clicked = null;
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, ComboData)
            .Add(x => x.OnSegmentClick, EventCallback.Factory.Create<ChartSegment>(this, s => clicked = s)));

        cut.FindAll("circle.tm-chart__point")[1].Click();

        clicked.Should().NotBeNull();
        clicked!.DatasetIndex.Should().Be(2); // the overlay dataset keeps its original index
        clicked.Index.Should().Be(1);
        clicked.Value.Should().Be(8);
    }

    [Fact]
    public void BarChart_Without_RenderAs_Keeps_Existing_Behavior()
    {
        var data = new ChartData
        {
            Labels = ["A", "B", "C"],
            Datasets =
            [
                new ChartDataset { Label = "One", Values = [1, 2, 3], Color = "#3b82f6" },
                new ChartDataset { Label = "Two", Values = [2, 3, 4], Color = "#ef4444" },
            ]
        };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        cut.FindAll("rect.tm-chart__bar").Count.Should().Be(6);
        cut.FindAll("polyline.tm-chart__line").Should().BeEmpty();
    }

    [Fact]
    public void ComboChart_All_Line_Datasets_Renders_No_Bars()
    {
        // Edge case: every dataset opts into the overlay — no bars, only lines.
        var data = new ChartData
        {
            Labels = ["A", "B"],
            Datasets =
            [
                new ChartDataset { Label = "L1", Values = [1, 2], Color = "#3b82f6", RenderAs = ChartDatasetRenderAs.Line },
                new ChartDataset { Label = "L2", Values = [2, 1], Color = "#ef4444", RenderAs = ChartDatasetRenderAs.Line },
            ]
        };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        cut.FindAll("rect.tm-chart__bar").Should().BeEmpty();
        cut.FindAll("polyline.tm-chart__line").Count.Should().Be(2);
    }

    [Fact]
    public void BarChart_With_Many_Labels_Thins_Axis_Labels()
    {
        // Edge case: dense categorical axis (e.g. 48 monthly points) must not render
        // every label on top of each other.
        var labels = Enumerable.Range(0, 48).Select(i => $"M{i}").ToArray();
        var data = new ChartData
        {
            Labels = labels,
            Datasets = [new ChartDataset { Label = "V", Values = Enumerable.Range(0, 48).Select(i => (double)i).ToArray(), Color = "#3b82f6" }]
        };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, data));

        cut.FindAll("text.tm-chart__label").Count.Should().BeLessThanOrEqualTo(13);
    }

    [Fact]
    public void BarChart_With_Few_Labels_Renders_All_Labels()
    {
        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Bar)
            .Add(x => x.Data, ComboData));

        cut.FindAll("text.tm-chart__label").Count.Should().Be(4);
    }

    [Fact]
    public void LineChart_Ignores_RenderAs_Line()
    {
        // RenderAs=Line on a Line chart is a no-op: the dataset renders as a normal line.
        var data = new ChartData
        {
            Labels = ["A", "B"],
            Datasets =
            [
                new ChartDataset { Label = "L", Values = [1, 2], Color = "#3b82f6", RenderAs = ChartDatasetRenderAs.Line },
            ]
        };

        var cut = RenderComponent<TmChart>(p => p
            .Add(x => x.Type, ChartType.Line)
            .Add(x => x.Data, data));

        cut.FindAll("polyline.tm-chart__line").Count.Should().Be(1);
    }
}
