using System.Globalization;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>bUnit tests for the TmSankeyChart SVG renderer and state handling.</summary>
public class TmSankeyChartTests : LocalizationTestBase
{
    [Fact]
    public void SankeyChart_RendersResponsiveAccessibleSvg()
    {
        var cut = RenderChart(ValidData());

        var svg = cut.Find("svg");
        svg.GetAttribute("viewBox").Should().Be("0 0 800 400");
        svg.GetAttribute("preserveAspectRatio").Should().Be("xMidYMid meet");
        svg.GetAttribute("role").Should().Be("img");
        svg.GetAttribute("aria-label").Should().Be("Sankey chart");
        svg.GetAttribute("width").Should().Be("100%");
        svg.GetAttribute("height").Should().Be("100%");
    }

    [Fact]
    public void SankeyChart_RendersOneNodeAndLinkElementPerInput()
    {
        var cut = RenderChart(ValidData());

        cut.FindAll("rect.tm-sankey__node").Should().HaveCount(3);
        cut.FindAll("path.tm-sankey__link").Should().HaveCount(2);
    }

    [Fact]
    public void SankeyChart_UsesCustomNodeColorAndPaletteFallback()
    {
        var cut = RenderChart(ValidData());
        var nodes = cut.FindAll("rect.tm-sankey__node");

        nodes[0].GetAttribute("fill").Should().Be("#123456");
        nodes[1].GetAttribute("fill").Should().Be("#ef4444");
    }

    [Fact]
    public void SankeyChart_RendersProportionalLinkWidthsAndSourceColor()
    {
        var cut = RenderChart(ValidData());
        var links = cut.FindAll("path.tm-sankey__link");
        var firstWidth = AttributeAsDouble(links[0], "stroke-width");
        var secondWidth = AttributeAsDouble(links[1], "stroke-width");

        secondWidth.Should().BeApproximately(firstWidth * 2, 0.002);
        links.Should().OnlyContain(link => link.GetAttribute("stroke") == "#123456");
    }

    [Fact]
    public void SankeyChart_LabelsIncludeValuesUsingCustomFormatter()
    {
        var cut = Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, ValidData())
            .Add(component => component.ValueFormatter, value => $"EUR {value:0.00}"));

        var labels = cut.FindAll("text.tm-sankey__label");
        labels.Should().HaveCount(3);
        labels.Select(label => label.TextContent).Should().Contain(
            "Income — EUR 30.00",
            "Housing — EUR 10.00",
            "Savings — EUR 20.00");
    }

    [Fact]
    public void SankeyChart_ShowValuesFalseRendersLabelsWithoutValues()
    {
        var cut = Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, ValidData())
            .Add(component => component.ShowValues, false));

        cut.FindAll("text.tm-sankey__label")
            .Select(label => label.TextContent)
            .Should().Equal("Income", "Housing", "Savings");
    }

    [Fact]
    public void SankeyChart_ShowLabelsFalseHidesLabels()
    {
        var cut = Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, ValidData())
            .Add(component => component.ShowLabels, false));

        cut.FindAll("text.tm-sankey__label").Should().BeEmpty();
    }

    [Fact]
    public void SankeyChart_ReservesHorizontalSpaceForEndpointLabels()
    {
        var cut = RenderChart(ValidData());
        var labels = cut.FindAll("text.tm-sankey__label");

        AttributeAsDouble(labels[0], "x").Should().BeGreaterThanOrEqualTo(120);
        AttributeAsDouble(labels[1], "x").Should().BeLessThanOrEqualTo(680);
        AttributeAsDouble(labels[2], "x").Should().BeLessThanOrEqualTo(680);
    }

    [Fact]
    public void SankeyChart_RendersNodeAndLinkTitles()
    {
        var cut = RenderChart(ValidData());

        cut.FindAll("rect.tm-sankey__node title")
            .Select(title => title.TextContent)
            .Should().Contain("Income — 30", "Housing — 10", "Savings — 20");
        cut.FindAll("path.tm-sankey__link title")
            .Select(title => title.TextContent)
            .Should().Contain("Income → Housing: 10", "Income → Savings: 20");
    }

    [Fact]
    public void SankeyChart_AppliesContainerOptionsAndLinkOpacity()
    {
        var cut = Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, ValidData())
            .Add(component => component.Width, "640px")
            .Add(component => component.Height, "320px")
            .Add(component => component.LinkOpacity, 0.25)
            .Add(component => component.Class, "cash-flow"));

        var root = cut.Find(".tm-sankey");
        root.ClassList.Should().Contain("cash-flow");
        root.GetAttribute("style").Should().Contain("width:640px").And.Contain("height:320px");
        cut.FindAll("path.tm-sankey__link")
            .Should().OnlyContain(link => link.GetAttribute("stroke-opacity") == "0.25");
    }

    [Fact]
    public void SankeyChart_NonFiniteOpacityAndBlankColorUseSafeDefaults()
    {
        var data = Data(
            [Node("A", color: " "), Node("B")],
            [Link("A", "B", 1)]);
        var cut = Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, data)
            .Add(component => component.LinkOpacity, double.NaN));

        cut.Find("rect.tm-sankey__node").GetAttribute("fill").Should().Be("#3b82f6");
        cut.Find("path.tm-sankey__link").GetAttribute("stroke-opacity").Should().Be("0.4");
    }

    [Fact]
    public void SankeyChart_EmptyDataRendersLocalizedNoDataState()
    {
        var cut = RenderChart(Data([], []));

        cut.Find(".tm-sankey__error").TextContent.Should().Be("No data available");
        cut.FindAll("svg").Should().BeEmpty();
    }

    [Fact]
    public void SankeyChart_CycleRendersLocalizedErrorWithoutThrowing()
    {
        var cut = RenderChart(Data(
            [Node("A"), Node("B")],
            [Link("A", "B", 1), Link("B", "A", 1)]));

        cut.Find(".tm-sankey__error").TextContent.Should().Be("Flow data contains a cycle");
        cut.FindAll("svg").Should().BeEmpty();
    }

    [Fact]
    public void SankeyChart_InvalidDataRendersLocalizedErrorWithoutThrowing()
    {
        var cut = RenderChart(Data(
            [Node("A")],
            [Link("A", "missing", 1)]));

        cut.Find(".tm-sankey__error").TextContent.Should().Be("Invalid flow data");
        cut.FindAll("svg").Should().BeEmpty();
    }

    private IRenderedComponent<TmSankeyChart> RenderChart(SankeyData data) =>
        Render<TmSankeyChart>(parameters => parameters
            .Add(component => component.Data, data));

    private static SankeyData ValidData() =>
        Data(
            [
                Node("income", "Income", "#123456"),
                Node("housing", "Housing"),
                Node("savings", "Savings"),
            ],
            [
                Link("income", "housing", 10),
                Link("income", "savings", 20),
            ]);

    private static SankeyData Data(
        IReadOnlyList<SankeyNode> nodes,
        IReadOnlyList<SankeyLink> links) =>
        new()
        {
            Nodes = nodes,
            Links = links,
        };

    private static SankeyNode Node(string id, string? label = null, string? color = null) =>
        new()
        {
            Id = id,
            Label = label ?? id,
            Color = color,
        };

    private static SankeyLink Link(string sourceId, string targetId, double value) =>
        new()
        {
            SourceId = sourceId,
            TargetId = targetId,
            Value = value,
        };

    private static double AttributeAsDouble(AngleSharp.Dom.IElement element, string name) =>
        double.Parse(element.GetAttribute(name)!, CultureInfo.InvariantCulture);
}
