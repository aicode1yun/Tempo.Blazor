using System.Globalization;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>
/// TDD tests for the Fáze 18 (C5) stacked chart types on TmChart: StackedBar (vertical),
/// StackedHorizontalBar and StackedArea. Each category's datasets accumulate on a shared
/// baseline — asserted via exact segment RATIOS and cumulative (contiguous, gap-free) offsets,
/// which are size-independent and platform-stable. Rendering reuses the existing TmChart SVG /
/// legend / tooltip / click infrastructure (no new component).
/// </summary>
public class TmChartStackedTests : LocalizationTestBase
{
    // Q1 stack total 60 (Alfa 40 + Beta 20), Q2 total 30 — Q1 is the tallest stack.
    private static ChartData StackData => new()
    {
        Labels = ["Q1", "Q2"],
        Datasets =
        [
            new ChartDataset { Label = "Alfa", Values = [40, 10], Color = "#3b82f6" },
            new ChartDataset { Label = "Beta", Values = [20, 20], Color = "#ef4444" }
        ]
    };

    private static double D(AngleSharp.Dom.IElement el, string name)
        => double.Parse(el.GetAttribute(name)!, CultureInfo.InvariantCulture);

    private IRenderedComponent<TmChart> Render(ChartType type)
        => Render<TmChart>(p =>
        {
            p.Add(x => x.Type, type);
            p.Add(x => x.Data, StackData);
        });

    [Fact]
    public void StackedBar_RendersOneSegmentPerDatasetPerCategory()
    {
        var cut = Render(ChartType.StackedBar);

        // 2 datasets × 2 categories, all positive → 4 stacked rects.
        cut.FindAll(".tm-chart__bar").Should().HaveCount(4);
    }

    [Fact]
    public void StackedBar_SegmentsStackCumulativelyWithExactAreaRatios()
    {
        var cut = Render(ChartType.StackedBar);
        var bars = cut.FindAll(".tm-chart__bar");

        // DOM render order is category-major: [Alfa Q1, Beta Q1, Alfa Q2, Beta Q2].
        var alfaQ1 = bars[0];
        var betaQ1 = bars[1];

        var alfaH = D(alfaQ1, "height");
        var betaH = D(betaQ1, "height");
        var alfaY = D(alfaQ1, "y");
        var betaY = D(betaQ1, "y");

        // Exact area ratio: Alfa(40) segment is twice the Beta(20) segment.
        (alfaH / betaH).Should().BeApproximately(2.0, 0.001);

        // Beta sits directly on top of Alfa with NO gap (cumulative, contiguous).
        (betaY + betaH).Should().BeApproximately(alfaY, 0.02);

        // Both segments share the same X band (stacked, not grouped side by side).
        D(betaQ1, "x").Should().BeApproximately(D(alfaQ1, "x"), 0.02);
        D(betaQ1, "width").Should().BeApproximately(D(alfaQ1, "width"), 0.02);

        // The full Q1 stack (Alfa bottom + Beta top) reaches the top of the tallest stack.
        betaY.Should().BeLessThan(alfaY);
    }

    [Fact]
    public void StackedHorizontalBar_SegmentsStackLeftToRightWithExactAreaRatios()
    {
        var cut = Render(ChartType.StackedHorizontalBar);
        var bars = cut.FindAll(".tm-chart__bar");
        bars.Should().HaveCount(4);

        var alfaQ1 = bars[0];
        var betaQ1 = bars[1];

        (D(alfaQ1, "width") / D(betaQ1, "width")).Should().BeApproximately(2.0, 0.001);
        // Beta starts exactly where Alfa ends (contiguous, no gap).
        D(betaQ1, "x").Should().BeApproximately(D(alfaQ1, "x") + D(alfaQ1, "width"), 0.02);
        // Same row band and height.
        D(betaQ1, "y").Should().BeApproximately(D(alfaQ1, "y"), 0.02);
        D(betaQ1, "height").Should().BeApproximately(D(alfaQ1, "height"), 0.02);
    }

    [Fact]
    public void StackedArea_RendersOneFilledPolygonPerDataset()
    {
        var cut = Render(ChartType.StackedArea);

        cut.FindAll(".tm-chart__stacked-area").Should().HaveCount(2);
    }

    [Fact]
    public void StackedBar_Click_RaisesSegmentWithDatasetAndCategory()
    {
        ChartSegment? clicked = null;
        var cut = Render<TmChart>(p =>
        {
            p.Add(x => x.Type, ChartType.StackedBar);
            p.Add(x => x.Data, StackData);
            p.Add(x => x.OnSegmentClick, seg => clicked = seg);
        });

        cut.FindAll(".tm-chart__bar")[1].Click(); // Beta Q1

        clicked.Should().NotBeNull();
        clicked!.DatasetIndex.Should().Be(1);
        clicked.Index.Should().Be(0);
        clicked.Value.Should().Be(20);
    }

    [Fact]
    public void StackedBar_InteractiveLegend_HidesDatasetSegments()
    {
        var cut = Render<TmChart>(p =>
        {
            p.Add(x => x.Type, ChartType.StackedBar);
            p.Add(x => x.Data, StackData);
            p.Add(x => x.InteractiveLegend, true);
        });

        cut.FindAll(".tm-chart__bar").Should().HaveCount(4);

        // Toggle the second dataset (Beta) off — its two segments disappear.
        cut.Find("[data-testid='chart-legend-1']").Click();

        cut.FindAll(".tm-chart__bar").Should().HaveCount(2);
    }
}
