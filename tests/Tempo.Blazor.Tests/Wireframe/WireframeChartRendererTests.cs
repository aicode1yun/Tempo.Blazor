using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Configuration;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// The wireframe stencil offers a chart type for every <c>ChartType</c> the real TmChart supports.
/// These tests pin the stacked variants to a genuinely stacked preview: a wireframe that silently
/// fell back to plain bars would mislead whoever picks the type in the designer.
/// </summary>
public class WireframeChartRendererTests
{
    // Third entry of the renderer's fill palette. It is only reachable when a shape is drawn per
    // series, so its presence proves the preview is segmented rather than a single-series bar.
    private const string ThirdSeriesFill = "#fef9c3";

    private static async Task<string> RenderChartAsync(string type, int dataPoints = 4)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTempoBlazorWireframe();
        using var provider = services.BuildServiceProvider();

        var element = new WireframeElement { Type = "TmChart", X = 0, Y = 0, W = 400, H = 240 };
        element.Props["type"] = JsonSerializer.SerializeToElement(type);
        element.Props["dataPoints"] = JsonSerializer.SerializeToElement(dataPoints);

        var page = new WireframePage { Name = "P", Width = 400, Height = 240 };
        page.Elements.Add(element);

        return await provider.GetRequiredService<IWireframeSvgRenderer>().RenderPageAsync(page);
    }

    [Theory]
    [InlineData("stackedBar")]
    [InlineData("stackedHorizontalBar")]
    public async Task StackedBarTypes_RenderSegmentedBars(string type)
    {
        var plain = await RenderChartAsync("bar");
        var stacked = await RenderChartAsync(type);

        plain.Should().NotContain(ThirdSeriesFill);
        stacked.Should().Contain(ThirdSeriesFill);
        CountOccurrences(stacked, "<rect").Should().BeGreaterThan(CountOccurrences(plain, "<rect"));
    }

    [Fact]
    public async Task StackedArea_RendersOneBandPerSeries()
    {
        var svg = await RenderChartAsync("stackedArea");

        CountOccurrences(svg, "<polygon").Should().Be(3);
        svg.Should().NotContain("<polyline");   // bands replace the single series line
        svg.Should().Contain(ThirdSeriesFill);
    }

    [Fact]
    public async Task HorizontalBar_RendersWithoutTheExplicitHorizontalProp()
    {
        var svg = await RenderChartAsync("horizontalBar");

        // Horizontal bars all start at the chart's left edge and differ in y, unlike vertical bars
        // which share a baseline and differ in x.
        CountOccurrences(svg, "x='32'").Should().BeGreaterThan(1);
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = value.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = value.IndexOf(token, index + token.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
