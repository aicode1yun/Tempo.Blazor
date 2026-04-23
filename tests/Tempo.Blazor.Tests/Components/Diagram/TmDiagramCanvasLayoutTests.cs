using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

/// <summary>
/// DOM-structure tests for <see cref="TmDiagramCanvas"/>.
///
/// <para>
/// After F2 (planning/DIAGRAM_UNIFIED_SVG_PLAN.md) the canvas renders a single
/// <c>&lt;svg&gt;</c> containing four direct <c>&lt;g&gt;</c> panes, modelled on
/// mxGraph / draw.io:
/// </para>
/// <list type="bullet">
///   <item><c>.tm-diagram-bg-pane</c> — background, grid, model-level group bounds</item>
///   <item><c>.tm-diagram-scene-pane</c> — edges + nodes (inside a <c>&lt;foreignObject&gt;</c>)</item>
///   <item><c>.tm-diagram-overlay-pane</c> — selection outlines, drop-target highlights (populated by JS)</item>
///   <item><c>.tm-diagram-decorator-pane</c> — resize/rotate handles, connect arrows (populated in F5)</item>
/// </list>
///
/// <para>
/// These tests lock that structure in place so later phases (F3 global Z-order,
/// F5 handle migration, F7 JS cleanup) can’t accidentally regress it.
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
    public void Canvas_HasFourPaneStructure_AfterF2()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        cut.Find(".tm-diagram-canvas").Should().NotBeNull();
        cut.Find(".tm-diagram-canvas__svg").Should().NotBeNull();
        cut.Find(".tm-diagram-bg-pane").Should().NotBeNull("bg-pane hosts grid + page frame + model group bounds");
        cut.Find(".tm-diagram-scene-pane").Should().NotBeNull("scene-pane hosts edges + the nodes foreignObject");
        cut.Find(".tm-diagram-overlay-pane").Should().NotBeNull("overlay-pane is reserved for JS-injected selection outlines");
        cut.Find(".tm-diagram-decorator-pane").Should().NotBeNull("decorator-pane is reserved for JS-injected handles (F5)");
    }

    [Fact]
    public void Canvas_InteractionDiv_NoLongerExists_AfterF2()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        cut.FindAll(".tm-diagram-canvas__interaction").Should().BeEmpty(
            "F2.12 removed the dead interaction layer; nothing ever wrote into it");
    }

    [Fact]
    public void Canvas_NodesLiveInsideForeignObject_InScenePane()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var scenePane = cut.Find(".tm-diagram-scene-pane");
        scenePane.OuterHtml.Should().Contain("tm-diagram-nodes-fo",
            "nodes are wrapped in a single <foreignObject> inside scene-pane (split per-node in F3)");
        scenePane.OuterHtml.Should().Contain("data-node-id",
            "node divs live inside that foreignObject during F2");
        scenePane.OuterHtml.Should().Contain("tm-diagram-edge",
            "edges render inside scene-pane, alongside (for now before) the nodes foreignObject");
    }

    [Fact]
    public void Canvas_ScenePaneEmitsEdgesBeforeNodes_InF2BaselineZOrder()
    {
        // F2 deliberately keeps the existing "edges below nodes" Z-order: the
        // foreignObject wrapping all nodes is placed AFTER the edges inside the
        // scene-pane. True interleaving comes in F3 once nodes become per-node
        // <g> elements. This test pins the current ordering so the F3 change is
        // explicit.
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var sceneHtml = cut.Find(".tm-diagram-scene-pane").OuterHtml;
        var firstEdge = sceneHtml.IndexOf("tm-diagram-edge-group", StringComparison.Ordinal);
        var nodesFo = sceneHtml.IndexOf("tm-diagram-nodes-fo", StringComparison.Ordinal);
        firstEdge.Should().BeGreaterThan(-1);
        nodesFo.Should().BeGreaterThan(-1);
        firstEdge.Should().BeLessThan(nodesFo, "edges must render before the nodes foreignObject");
    }

    [Fact]
    public void Canvas_SvgHasNoInlineScaleTransform_AfterF2()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find(".tm-diagram-canvas__svg");
        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().NotContain("transform:scale(",
            "F2.5 removed the duplicate CSS scale; Page.Scale is now folded into the viewBox / zoomTo");
        style.Should().NotContain("transform-origin",
            "no CSS transform on the SVG anymore, so no need for an origin override");
    }

    [Fact]
    public void Canvas_NodesStillUseCssTranslateTransform_InF2()
    {
        // F2 keeps nodes as HTML inside a foreignObject; F3 will move them onto
        // SVG <g transform>. Until then the CSS translate string format is still
        // the contract parsed by diagram-editor.js::_nodeRect.
        var doc = BuildSampleDocument();
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var nodeEl = cut.Find($"[data-node-id='{doc.Nodes[0].Id}']");
        var style = nodeEl.GetAttribute("style") ?? string.Empty;
        style.Should().Contain("translate(",
            "F3 will replace this with an SVG <g transform>; until then the CSS format is the JS contract");
    }

    [Fact]
    public void Canvas_ViewBoxMatchesDocumentSize_WhenPageViewDisabled_AtUnitScale()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, new DiagramDocument { Width = 1234, Height = 567 })
            .Add(c => c.ShowPageView, false));

        cut.Find("svg").GetAttribute("viewBox").Should().Be("0 0 1234 567");
    }

    [Fact]
    public void Canvas_NodesForeignObject_MatchesDocumentSize()
    {
        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, new DiagramDocument { Width = 1234, Height = 567 })
            .Add(c => c.ShowPageView, false));

        var fo = cut.Find(".tm-diagram-nodes-fo");
        fo.GetAttribute("width").Should().Be("1234");
        fo.GetAttribute("height").Should().Be("567");
    }
}
