using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetStatusBarTests : LocalizationTestBase
{
    [Fact]
    public void Render_ShowsContainer()
    {
        var cut = Render<TmSpreadsheetStatusBar>();

        cut.FindAll(".tm-spreadsheet-statusbar").Count.Should().Be(1);
    }

    [Fact]
    public void Render_HasZoomControl()
    {
        var cut = Render<TmSpreadsheetStatusBar>(p => p.Add(x => x.Zoom, 1.0));

        cut.FindAll(".tm-spreadsheet-statusbar__zoom").Count.Should().Be(1);
        cut.FindAll(".tm-spreadsheet-statusbar__zoom-slider").Count.Should().Be(1);
        cut.Find(".tm-spreadsheet-statusbar__zoom-percent").TextContent.Should().Contain("100%");
    }

    [Fact]
    public void RangeWithNumbers_ShowsLocalizedAggregations()
    {
        var aggregation = new SpreadsheetAggregationResult(
            Count: 3, CountNumbers: 3, Sum: 60.0, Average: 20.0, Min: 10.0, Max: 30.0);

        var cut = Render<TmSpreadsheetStatusBar>(p => p.Add(x => x.Aggregation, aggregation));

        var text = cut.Find(".tm-spreadsheet-statusbar__aggregations").TextContent;
        text.Should().Contain("Sum");
        text.Should().Contain("60");
        text.Should().Contain("Average");
        text.Should().Contain("20");
        text.Should().Contain("Count");
        text.Should().Contain("3");
    }

    [Fact]
    public void SingleCell_HidesAggregations()
    {
        var aggregation = new SpreadsheetAggregationResult(
            Count: 1, CountNumbers: 1, Sum: 5.0, Average: 5.0, Min: 5.0, Max: 5.0);

        var cut = Render<TmSpreadsheetStatusBar>(p => p.Add(x => x.Aggregation, aggregation));

        cut.Find(".tm-spreadsheet-statusbar__aggregations").TextContent.Trim().Should().BeEmpty();
    }

    [Fact]
    public void AllText_HidesNumericAggregationsButShowsCount()
    {
        var aggregation = new SpreadsheetAggregationResult(
            Count: 3, CountNumbers: 0, Sum: null, Average: null, Min: null, Max: null);

        var cut = Render<TmSpreadsheetStatusBar>(p => p.Add(x => x.Aggregation, aggregation));

        var text = cut.Find(".tm-spreadsheet-statusbar__aggregations").TextContent;
        text.Should().Contain("Count");
        text.Should().Contain("3");
        text.Should().NotContain("Sum");
        text.Should().NotContain("Average");
    }

    [Fact]
    public void ZoomSlider_Input_FiresOnZoomChanged()
    {
        double? received = null;
        var cut = Render<TmSpreadsheetStatusBar>(p => p
            .Add(x => x.Zoom, 1.0)
            .Add(x => x.OnZoomChanged, EventCallback.Factory.Create<double>(this, z => received = z)));

        cut.Find(".tm-spreadsheet-statusbar__zoom-slider").Input("150");

        received.Should().Be(1.5);
    }

    [Fact]
    public void ZoomPercent_Click_ResetsTo100()
    {
        double? received = null;
        var cut = Render<TmSpreadsheetStatusBar>(p => p
            .Add(x => x.Zoom, 1.5)
            .Add(x => x.OnZoomChanged, EventCallback.Factory.Create<double>(this, z => received = z)));

        cut.Find(".tm-spreadsheet-statusbar__zoom-percent").Click();

        received.Should().Be(1.0);
    }

    [Fact]
    public void ZoomIn_Click_IncreasesByTenPercent()
    {
        double? received = null;
        var cut = Render<TmSpreadsheetStatusBar>(p => p
            .Add(x => x.Zoom, 1.0)
            .Add(x => x.OnZoomChanged, EventCallback.Factory.Create<double>(this, z => received = z)));

        cut.FindAll(".tm-spreadsheet-statusbar__zoom-btn")[1].Click();

        received.Should().Be(1.1);
    }
}
