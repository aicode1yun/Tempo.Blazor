using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

/// <summary>
/// Safety-net tests for the unified SVG canvas refactor (see planning/DIAGRAM_UNIFIED_SVG_PLAN.md).
///
/// <para>
/// These tests lock down the <b>current</b> two-layer DOM structure (SVG + HTML overlay) of
/// <see cref="TmDiagramCanvas"/> so that the refactor to a single SVG scene can be caught early
/// if an intermediate step accidentally changes the layout in a way the plan does not account for.
/// </para>
///
/// <para>
/// After F2 (move nodes into a foreignObject inside the SVG) these tests will be <b>updated</b>
/// to assert the new 4-pane structure (<c>.tm-diagram-bg-pane</c>, <c>.tm-diagram-scene-pane</c>,
/// <c>.tm-diagram-overlay-pane</c>, <c>.tm-diagram-decorator-pane</c>). Until then they serve as
/// a regression alarm.
/// </para>
/// </summary>
public class TmDiagramCanvasLayoutTests : LocalizationTestBase
{
    public TmDiagramCanvasLayoutTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    private static DiagramDocument BuildSampleDocument()
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var a = new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 120, H = 60, ZIndex = 1 };
        var b = new DiagramNode { StencilId = "general.rectangle", X = 300, Y = 100, W = 120, H = 60, ZIndex = 1 };
        doc.Nodes.Add(a);
        doc.Nodes.Add(b);
        doc.Edges.Add(new DiagramEdge { SourceNodeId = a.Id, TargetNodeId = b.Id, ZIndex = 2 });
        return doc;
    }

    [Fact]
    public void Canvas_HasDocumentedLayerStructure_Baseline()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var root = cut.Find(".tm-diagram-canvas");
        root.Should().NotBeNull();

        var svg = cut.Find(".tm-diagram-canvas__svg");
        svg.Should().NotBeNull("the SVG layer renders grid + background + edges today");

        var overlay = cut.Find(".tm-diagram-canvas__overlay");
        overlay.Should().NotBeNull("the HTML overlay hosts nodes in the baseline layout");

        var transformLayer = cut.Find(".tm-diagram-canvas__overlay .tm-diagram-transform-layer");
        transformLayer.Should().NotBeNull("the transform wrapper is applied by JS to keep HTML in sync with SVG zoom/pan");

        var interaction = cut.Find(".tm-diagram-canvas__interaction");
        interaction.Should().NotBeNull("the interaction layer is reserved for JS-injected outlines");
    }

    [Fact]
    public void Canvas_BaselineZOrder_AllNodesLiveAboveAllEdges()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find(".tm-diagram-canvas__svg");
        var overlay = cut.Find(".tm-diagram-canvas__overlay");

        svg.OuterHtml.Should().Contain("tm-diagram-edge", "edges are rendered inside the SVG layer today");
        overlay.OuterHtml.Should().Contain("data-node-id", "nodes are rendered inside the HTML overlay today");
    }

    [Fact]
    public void Canvas_SvgHasInlineScaleTransform_Baseline()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find(".tm-diagram-canvas__svg");
        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().Contain("transform:scale(",
            "today the SVG carries a duplicate CSS scale transform; F2 will remove it in favor of viewBox-only zoom");
    }

    [Fact]
    public void Canvas_NodesHaveCssTranslateTransform_Baseline()
    {
        var doc = BuildSampleDocument();
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var nodeEl = cut.Find($"[data-node-id='{doc.Nodes[0].Id}']");
        var style = nodeEl.GetAttribute("style") ?? string.Empty;
        style.Should().Contain("translate(",
            "today node position uses CSS translate; F3 will move this onto an SVG <g transform> attribute");
    }

    [Fact]
    public void Canvas_ViewBoxMatchesDocumentSize_WhenPageViewDisabled()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, new DiagramDocument { Width = 1234, Height = 567 })
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        svg.GetAttribute("viewBox").Should().Be("0 0 1234 567");
    }
}
