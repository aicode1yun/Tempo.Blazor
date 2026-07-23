using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>TDD tests for the TmChart Area type (TMPO-003).</summary>
public class TmChartAreaTests : LocalizationTestBase
{
    private static ChartData SimpleAreaData => new()
    {
        Labels = ["Jan", "Feb", "Mar", "Apr"],
        Datasets =
        [
            new ChartDataset { Label = "Rent", Values = [10, 20, 15, 30], Color = "#3b82f6" }
        ]
    };

    private static ChartData MultiSeriesData => new()
    {
        Labels = ["Q1", "Q2", "Q3"],
        Datasets =
        [
            new ChartDataset { Label = "2024", Values = [100, 200, 150], Color = "#3b82f6" },
            new ChartDataset { Label = "2025", Values = [120, 180, 220], Color = "#ef4444" }
        ]
    };

    // ── ARE-1: Area vykreslí uzavřenou plochu (path s Z) ───────────────────

    [Fact]
    public void Area_RendersClosedAreaPath()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        var area = cut.Find("path.tm-chart__area");
        area.GetAttribute("d").Should().NotBeNullOrEmpty();
        area.GetAttribute("d")!.TrimEnd().Should().EndWith(
            "L 580,360 L 50,360 Z",
            "a positive-only area must close from the last X back to the first X on the zero baseline");
    }

    // ── ARE-2: plocha je doplněná linií přes vrcholy (vizuální konzistence s Line) ──

    [Fact]
    public void Area_RendersTopLineAndPoints()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        cut.FindAll("polyline.tm-chart__line").Should().HaveCount(1);
        cut.FindAll("circle.tm-chart__point").Should().HaveCount(4);
    }

    // ── ARE-3: více sérií → jedna plocha na sérii ──────────────────────────

    [Fact]
    public void Area_MultipleSeries_RenderOneAreaEach()
    {
        var data = MultiSeriesData with
        {
            Datasets =
            [
                MultiSeriesData.Datasets[0] with { BackgroundColor = "#bfdbfe" },
                MultiSeriesData.Datasets[1]
            ]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        cut.FindAll("path.tm-chart__area")
            .Select(area => area.GetAttribute("fill"))
            .Should().Equal(["#bfdbfe", "#ef4444"]);
        cut.FindAll("polyline.tm-chart__line").Should().HaveCount(2);
        cut.FindAll("circle.tm-chart__point").Should().HaveCount(6);
    }

    // ── ARE-4: prázdná série → žádná plocha, žádný pád ─────────────────────

    [Fact]
    public void Area_EmptySeries_RendersNothingWithoutCrash()
    {
        var data = new ChartData
        {
            Labels = ["Jan"],
            Datasets = [new ChartDataset { Label = "Empty", Values = [], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        cut.FindAll("path.tm-chart__area").Should().BeEmpty();
    }

    // ── ARE-5: 1bodová série → bod + úsečka k ose, žádná plocha ────────────

    [Fact]
    public void Area_SinglePoint_RendersPointWithStem()
    {
        var data = new ChartData
        {
            Labels = ["Jan"],
            Datasets = [new ChartDataset { Label = "One", Values = [42], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        cut.FindAll("path.tm-chart__area").Should().BeEmpty();
        cut.FindAll("circle.tm-chart__point").Should().HaveCount(1);
        cut.FindAll("line.tm-chart__area-stem").Should().HaveCount(1);
    }

    // ── ARE-6: záporné hodnoty — plocha k nulové ose, ne ke dnu grafu ──────

    [Fact]
    public void Area_NegativeValues_FillToZeroBaseline_NotChartBottom()
    {
        // Domain [-10, 10]: zero baseline = PT + CH/2 = 20 + 170 = 190 (chart bottom is 360).
        var data = new ChartData
        {
            Labels = ["A", "B"],
            Datasets = [new ChartDataset { Label = "PnL", Values = [10, -10], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        var d = cut.Find("path.tm-chart__area").GetAttribute("d")!;
        // Data line: (50,20) → (580,360); closing baseline runs along y=190 (zero), not y=360 (bottom).
        d.Should().EndWith("L 580,190 L 50,190 Z", "the area must close on the zero baseline, not the chart bottom");
    }

    // ── ARE-7: záporné hodnoty se objeví na ose (grid popisky) ─────────────

    [Fact]
    public void Area_NegativeValues_AxisShowsNegativeLabels()
    {
        var data = new ChartData
        {
            Labels = ["A", "B"],
            Datasets = [new ChartDataset { Label = "PnL", Values = [10, -10], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data)
            .Add(x => x.ShowGrid, true));

        cut.Markup.Should().Contain("-10");
    }

    // ── ARE-8: chybějící hodnoty (NaN) přeruší plochu ──────────────────────

    [Fact]
    public void Area_MissingValues_BreakAreaIntoSegments()
    {
        var data = new ChartData
        {
            Labels = ["A", "B", "C", "D", "E"],
            Datasets = [new ChartDataset { Label = "Gaps", Values = [1, 2, double.NaN, 3, 4], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        cut.FindAll("path.tm-chart__area").Should().HaveCount(2);
        // The NaN value renders no point.
        cut.FindAll("circle.tm-chart__point").Should().HaveCount(4);
    }

    // ── ARE-9: všechny hodnoty stejné → plocha se vykreslí bez pádu ────────

    [Fact]
    public void Area_AllValuesEqual_RendersWithoutCrash()
    {
        var data = new ChartData
        {
            Labels = ["A", "B", "C"],
            Datasets = [new ChartDataset { Label = "Flat", Values = [5, 5, 5], Color = "#3b82f6" }]
        };

        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, data));

        cut.FindAll("path.tm-chart__area").Should().HaveCount(1);
    }

    // ── ARE-10: klik na bod vyvolá OnSegmentClick ──────────────────────────

    [Fact]
    public void Area_PointClick_RaisesOnSegmentClick()
    {
        ChartSegment? clicked = null;
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData)
            .Add(x => x.OnSegmentClick, seg => clicked = seg));

        cut.FindAll("circle.tm-chart__point")[1].Click();

        clicked.Should().NotBeNull();
        clicked!.Index.Should().Be(1);
        clicked.Value.Should().Be(20);
        clicked.Label.Should().Be("Feb");
    }

    // ── ARE-11: legenda zobrazuje série ────────────────────────────────────

    [Fact]
    public void Area_Legend_ShowsDatasetLabels()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, MultiSeriesData)
            .Add(x => x.ShowLegend, true));

        var labels = cut.FindAll(".tm-chart__legend-label");
        labels.Should().HaveCount(2);
        labels[0].TextContent.Should().Be("2024");
        labels[1].TextContent.Should().Be("2025");
    }

    // ── ARE-12: interaktivní legenda skryje sérii (a její plochu) ──────────

    [Fact]
    public void Area_InteractiveLegend_HidesSeries()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, MultiSeriesData)
            .Add(x => x.InteractiveLegend, true));

        cut.Find("[data-testid='chart-legend-0']").Click();

        cut.FindAll("path.tm-chart__area").Should().HaveCount(1);
    }

    // ── ARE-13: volitelný gradient → linearGradient defs + fill url() ──────

    [Fact]
    public void Area_WithGradient_UsesLinearGradientFill()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData)
            .Add(x => x.AreaGradient, true));

        cut.FindAll("linearGradient").Should().HaveCount(1);
        cut.Find("path.tm-chart__area").GetAttribute("fill").Should().StartWith("url(#");
    }

    // ── ARE-14: bez gradientu je fill barvou série ─────────────────────────

    [Fact]
    public void Area_WithoutGradient_FillsWithSeriesColor()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        cut.Find("path.tm-chart__area").GetAttribute("fill").Should().Be("#3b82f6");
    }

    [Fact]
    public void Area_CustomOpacity_AppliesToSolidArea()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData)
            .Add(x => x.AreaOpacity, 0.42));

        var area = cut.Find("path.tm-chart__area");
        area.GetAttribute("fill-opacity").Should().Be("0.42");
        area.GetAttribute("style").Should().Contain("--tm-chart-area-opacity:0.42");
    }

    [Fact]
    public void Area_DefaultOpacity_RemainsThemeTokenDriven()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        var area = cut.Find("path.tm-chart__area");
        area.HasAttribute("fill-opacity").Should().BeFalse();
        area.HasAttribute("style").Should().BeFalse();
    }

    [Fact]
    public void Area_ShowValues_RendersDatasetValuesAndXAxisLabels()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData)
            .Add(x => x.ShowValues, true));

        cut.FindAll("text.tm-chart__value")
            .Select(value => value.TextContent)
            .Should().Equal(["10", "20", "15", "30"]);
        cut.FindAll("text.tm-chart__label")
            .Select(label => label.TextContent)
            .Should().Equal(["Jan", "Feb", "Mar", "Apr"]);
    }

    // ── ARE-15: zpětná kompatibilita — Line nevykresluje žádnou plochu ─────

    [Fact]
    public void Line_DoesNotRenderAreaPath()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Line)
            .Add(x => x.Data, SimpleAreaData));

        cut.FindAll("path.tm-chart__area").Should().BeEmpty();
        cut.FindAll("polyline.tm-chart__line").Should().HaveCount(1);
    }

    // ── ARE-16: role=img + aria-label na SVG ───────────────────────────────

    [Fact]
    public void Area_Svg_HasImgRoleAndAriaLabel()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        var svg = cut.Find("svg");
        svg.GetAttribute("role").Should().Be("img");
        svg.GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    }

    // ── ARE-17: hover na bod zobrazí tooltip ───────────────────────────────

    [Fact]
    public void Area_PointHover_ShowsTooltip()
    {
        var cut = Render<TmChart>(p => p
            .Add(x => x.Type, ChartType.Area)
            .Add(x => x.Data, SimpleAreaData));

        cut.FindAll("circle.tm-chart__point")[2].TriggerEvent("onmouseover",
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Find("[data-testid='chart-tooltip']").TextContent.Should().Contain("Mar");
    }
}
