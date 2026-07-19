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
/// After F3 (planning/DIAGRAM_UNIFIED_SVG_PLAN.md) the canvas renders a single
/// <c>&lt;svg&gt;</c> containing four direct <c>&lt;g&gt;</c> panes, modelled on
/// mxGraph / draw.io:
/// </para>
/// <list type="bullet">
///   <item><c>.tm-diagram-bg-pane</c> — background, grid, model-level group bounds (F6)</item>
///   <item><c>.tm-diagram-scene-pane</c> — edges + per-node <c>&lt;g class="tm-diagram-node"&gt;</c> elements</item>
///   <item><c>.tm-diagram-overlay-pane</c> — selection outlines, drop-target highlights</item>
///   <item><c>.tm-diagram-decorator-pane</c> — resize/rotate handles, connect arrows (F5)</item>
/// </list>
///
/// <para>
/// These tests lock that structure in place so later phases (F5 handle migration,
/// F7 JS cleanup) can’t accidentally regress it.
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
        var cut = Render<TmDiagramCanvas>(p => p
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
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        cut.FindAll(".tm-diagram-canvas__interaction").Should().BeEmpty(
            "F2.12 removed the dead interaction layer; nothing ever wrote into it");
    }

    [Fact]
    public void Canvas_NodesArePerNodeSvgGroups_InScenePane()
    {
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var scenePane = cut.Find(".tm-diagram-scene-pane");
        scenePane.OuterHtml.Should().NotContain("tm-diagram-nodes-fo",
            "F3 eliminated the single document-sized nodes foreignObject");
        scenePane.OuterHtml.Should().NotContain("tm-diagram-canvas__overlay",
            "F3 eliminated the HTML transform-layer wrapper");

        var nodes = scenePane.QuerySelectorAll("g.tm-diagram-node[data-node-id]");
        nodes.Should().HaveCount(2, "each node now renders as its own <g class=\"tm-diagram-node\">");

        foreach (var n in nodes)
        {
            n.GetAttribute("transform").Should().Contain("translate(",
                "F3 positions nodes via SVG transform, not CSS transform");
            var inner = n.QuerySelector("foreignObject.tm-diagram-node__fo > div.tm-diagram-node__body");
            inner.Should().NotBeNull("rich HTML content lives inside a shape-sized foreignObject");
        }

        scenePane.OuterHtml.Should().Contain("tm-diagram-edge",
            "edges still render inside scene-pane alongside the node <g> elements");
    }

    [Fact]
    public void Canvas_ScenePane_InterleavesNodesAndEdges_ByZIndex_ThenKind_ThenInsertion()
    {
        // F3.B: the scene pane emits a single foreach over a sorted scene list
        // whose keys are (ZIndex, kind priority [node=0, edge=1], insertion index).
        //   - ZIndex 1 node
        //   - ZIndex 2 edge (originally rendered before the node pre-F3.B)
        // After F3.B the node (ZIndex 1) precedes the edge (ZIndex 2).
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        var sceneHtml = cut.Find(".tm-diagram-scene-pane").OuterHtml;
        var firstNode = sceneHtml.IndexOf("class=\"tm-diagram-node ", StringComparison.Ordinal);
        if (firstNode < 0)
            firstNode = sceneHtml.IndexOf("class=\"tm-diagram-node\"", StringComparison.Ordinal);
        var firstEdge = sceneHtml.IndexOf("tm-diagram-edge-group", StringComparison.Ordinal);
        firstNode.Should().BeGreaterThan(-1);
        firstEdge.Should().BeGreaterThan(-1);
        firstNode.Should().BeLessThan(firstEdge,
            "ZIndex 1 node renders before a ZIndex 2 edge in F3.B interleaved order");
    }

    [Fact]
    public void Canvas_ScenePane_Interleaves_EdgeAboveNode_WhenEdgeZIndexIsHigher()
    {
        // New capability enabled by F3.B — an edge can be placed above a node
        // in Z-order simply by giving it a higher ZIndex than the node.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var a = new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 120, H = 60, ZIndex = 5 };
        var b = new DiagramNode { StencilId = "general.rectangle", X = 300, Y = 100, W = 120, H = 60, ZIndex = 5 };
        doc.Nodes.Add(a);
        doc.Nodes.Add(b);
        doc.Edges.Add(new DiagramEdge { SourceNodeId = a.Id, TargetNodeId = b.Id, ZIndex = 10 });

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var sceneHtml = cut.Find(".tm-diagram-scene-pane").OuterHtml;
        var edgePos = sceneHtml.IndexOf("tm-diagram-edge-group", StringComparison.Ordinal);
        var lastNode = sceneHtml.LastIndexOf("class=\"tm-diagram-node ", StringComparison.Ordinal);
        if (lastNode < 0)
            lastNode = sceneHtml.LastIndexOf("class=\"tm-diagram-node\"", StringComparison.Ordinal);
        edgePos.Should().BeGreaterThan(-1);
        lastNode.Should().BeGreaterThan(-1);
        edgePos.Should().BeGreaterThan(lastNode,
            "edge with higher ZIndex must render after (above) nodes with lower ZIndex");
    }

    [Fact]
    public void Canvas_ScenePane_StableTieBreak_AtSameZIndex_UsesInsertionOrder()
    {
        // Same ZIndex across both kinds (nodes and edges): nodes come first
        // (kind priority), then edges; within each kind insertion order wins.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 50, Y = 50, W = 80, H = 50, ZIndex = 1 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 50, W = 80, H = 50, ZIndex = 1 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var html = cut.Find(".tm-diagram-scene-pane").OuterHtml;
        var i1 = html.IndexOf($"data-node-id=\"{n1.Id}\"", StringComparison.Ordinal);
        var i2 = html.IndexOf($"data-node-id=\"{n2.Id}\"", StringComparison.Ordinal);
        i1.Should().BeGreaterThan(-1);
        i2.Should().BeGreaterThan(-1);
        i1.Should().BeLessThan(i2, "at equal ZIndex the node inserted first renders first");
    }

    [Fact]
    public void Canvas_SvgHasNoInlineScaleTransform_AfterF2()
    {
        var cut = Render<TmDiagramCanvas>(p => p
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
    public void Canvas_NodesUseSvgTransform_AfterF3()
    {
        // F3 moved node position/rotation onto the SVG <g transform> attribute;
        // diagram-editor.js::_nodeRect now reads from that attribute (not from
        // the old CSS transform on the inner <div>). This test pins that contract.
        var doc = BuildSampleDocument();
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var nodeEl = cut.Find($"g.tm-diagram-node[data-node-id='{doc.Nodes[0].Id}']");
        var transform = nodeEl.GetAttribute("transform") ?? string.Empty;
        transform.Should().Contain("translate(",
            "F3 moved position onto the SVG <g> transform attribute");
        transform.Should().Contain("rotate(",
            "F3 also folds rotation into the SVG transform (around the node centre)");

        var style = nodeEl.GetAttribute("style") ?? string.Empty;
        style.Should().NotContain("translate(",
            "F3 removed the CSS translate fallback");
    }

    [Fact]
    public void Canvas_ViewBoxMatchesDocumentSize_WhenPageViewDisabled_AtUnitScale()
    {
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, new DiagramDocument { Width = 1234, Height = 567 })
            .Add(c => c.ShowPageView, false));

        cut.Find("svg").GetAttribute("viewBox").Should().Be("0 0 1234 567");
    }

    [Fact]
    public void Canvas_NodeForeignObject_IsShapeSized_AfterF3()
    {
        // After F3 the per-node <foreignObject> is sized to the node itself
        // (W × H), not to the whole document.
        var doc = new DiagramDocument { Width = 1234, Height = 567 };
        doc.Nodes.Add(new DiagramNode { StencilId = "general.rectangle", X = 10, Y = 20, W = 150, H = 80 });
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var fo = cut.Find("g.tm-diagram-node foreignObject.tm-diagram-node__fo");
        fo.GetAttribute("width").Should().Be("150");
        fo.GetAttribute("height").Should().Be("80");
        fo.GetAttribute("x").Should().Be("0");
        fo.GetAttribute("y").Should().Be("0");
    }

    // ── F3.E — native SVG shape primitives for simple stencils ──────────────

    [Fact]
    public void SimpleStencil_RendersAsNativeSvgRect_InsideNodeGroup()
    {
        // general.rectangle is a simple rectangle stencil — after F3.E it emits
        // a native <rect class="tm-diagram-node__shape-bg"> inside the node <g>,
        // in addition to the foreignObject that carries HTML content.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var node = new DiagramNode { StencilId = "general.rectangle", X = 10, Y = 20, W = 150, H = 80 };
        doc.Nodes.Add(node);
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var g = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        g.GetAttribute("data-shape-kind").Should().Be("rectangle");

        var shape = g.QuerySelector("rect.tm-diagram-node__shape-bg");
        shape.Should().NotBeNull("F3.E emits a native <rect> for rectangle stencils");
        shape!.GetAttribute("width").Should().Be("150");
        shape.GetAttribute("height").Should().Be("80");

        // The HTML content body must also be marked as native-shape-backed so
        // CSS can suppress the duplicate HTML background / border.
        var body = g.QuerySelector("foreignObject .tm-diagram-node__body");
        body.Should().NotBeNull();
        body!.GetAttribute("data-native-shape").Should().Be("true");
    }

    [Fact]
    public void EllipseStencil_RendersAsNativeSvgEllipse_InsideNodeGroup()
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var node = new DiagramNode { StencilId = "general.ellipse", X = 0, Y = 0, W = 100, H = 60 };
        doc.Nodes.Add(node);
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var g = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        g.GetAttribute("data-shape-kind").Should().Be("ellipse");
        var shape = g.QuerySelector("ellipse.tm-diagram-node__shape-bg");
        shape.Should().NotBeNull();
        shape!.GetAttribute("rx").Should().Be("50");
        shape.GetAttribute("ry").Should().Be("30");
    }

    [Fact]
    public void DiamondStencil_RendersAsNativeSvgPolygon_InsideNodeGroup()
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var node = new DiagramNode { StencilId = "general.rhombus", X = 0, Y = 0, W = 120, H = 80 };
        doc.Nodes.Add(node);
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var g = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        g.GetAttribute("data-shape-kind").Should().Be("diamond");
        var shape = g.QuerySelector("polygon.tm-diagram-node__shape-bg");
        shape.Should().NotBeNull();
        shape!.GetAttribute("points").Should().Contain("60,0").And.Contain("120,40").And.Contain("60,80").And.Contain("0,40");
    }

    [Fact]
    public void ComplexStencil_FallsBackToForeignObject_WithoutNativeShape()
    {
        // Stencils whose BackgroundShape is not mapped to a DiagramShapeKind
        // primitive (e.g. cylinder, cloud, actor, cube, sticky-note …) must
        // still render as HTML inside the foreignObject — no native SVG
        // primitive gets emitted, and the body is NOT marked as native-shape.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        // general.actor uses BackgroundShape="actor" which is NOT in the Kind map.
        var node = new DiagramNode { StencilId = "general.actor", X = 0, Y = 0, W = 60, H = 120 };
        doc.Nodes.Add(node);
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var g = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        g.GetAttribute("data-shape-kind").Should().Be("custom");

        // No native SVG primitive in the node <g>.
        g.QuerySelectorAll(".tm-diagram-node__shape-bg").Should().BeEmpty(
            "complex stencils keep the HTML/CSS fallback inside foreignObject");

        // Body is marked as NOT native-shape-backed, so HTML shape styling
        // (background/border) remains active.
        var body = g.QuerySelector("foreignObject .tm-diagram-node__body");
        body.Should().NotBeNull();
        body!.GetAttribute("data-native-shape").Should().Be("false");
    }

    [Fact]
    public void SimpleBackgroundShape_ReturnsNativeKind_EvenWhenShapeSvgIsSet()
    {
        // Every built-in simple stencil ships a ShapeSvg fallback that mirrors
        // its BackgroundShape. F3.E prefers the native SVG primitive over the
        // legacy fallback — the ShapeSvg is only used for Custom-kind stencils.
        var layout = new DiagramStencilLayout
        {
            BackgroundShape = "rectangle",
            ShapeSvg = "<rect x='0' y='0' width='100' height='100' fill='var(--stencil-fill)' />"
        };
        layout.GetNativeShapeKind().Should().Be(DiagramShapeKind.Rectangle);
    }

    [Fact]
    public void DiagramShapeKindExtensions_MapsKnownBackgroundShapes()
    {
        new DiagramStencilLayout { BackgroundShape = "rectangle" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Rectangle);
        new DiagramStencilLayout { BackgroundShape = "rounded" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.RoundedRectangle);
        new DiagramStencilLayout { BackgroundShape = "ellipse" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Ellipse);
        new DiagramStencilLayout { BackgroundShape = "circle" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Ellipse);
        new DiagramStencilLayout { BackgroundShape = "diamond" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Diamond);
        new DiagramStencilLayout { BackgroundShape = "triangle" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Triangle);
        new DiagramStencilLayout { BackgroundShape = "hexagon" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Hexagon);
        new DiagramStencilLayout { BackgroundShape = "parallelogram" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Parallelogram);

        new DiagramStencilLayout { BackgroundShape = "cylinder" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
        new DiagramStencilLayout { BackgroundShape = "cloud" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
        new DiagramStencilLayout { BackgroundShape = "actor" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
        new DiagramStencilLayout { BackgroundShape = "cube" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
        new DiagramStencilLayout { BackgroundShape = "sticky-note" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
        new DiagramStencilLayout { BackgroundShape = "document" }.GetNativeShapeKind().Should().Be(DiagramShapeKind.Custom);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // F6 — Group bounds are Razor-rendered SVG <rect class="tm-diagram-group-bounds">
    //      inside <g class="tm-diagram-bg-pane">, driven directly from the model
    //      (Document.Nodes grouped by GroupId). There's no JS imperative path.
    // ──────────────────────────────────────────────────────────────────────────

    private static DiagramDocument BuildGroupedDocument()
    {
        // Two members of group "g1" and one ungrouped spectator — the bounds
        // should wrap (100,100)–(420,300) plus an 8 px pad.
        var doc = new DiagramDocument { Width = 1200, Height = 800 };
        doc.Nodes.Add(new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 120, H = 60, GroupId = "g1" });
        doc.Nodes.Add(new DiagramNode { StencilId = "general.rectangle", X = 300, Y = 240, W = 120, H = 60, GroupId = "g1" });
        doc.Nodes.Add(new DiagramNode { StencilId = "general.rectangle", X = 700, Y = 400, W = 120, H = 60 });
        return doc;
    }

    [Fact]
    public void GroupedNodes_RenderGroupBounds_AsSvgRect_InsideBgPane()
    {
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildGroupedDocument())
            .Add(c => c.ShowPageView, false));

        var bgPane = cut.Find(".tm-diagram-bg-pane");
        var bounds = bgPane.QuerySelectorAll("rect.tm-diagram-group-bounds");
        bounds.Should().HaveCount(1, "F6 — one bounds rect per GroupId present in the model");

        var b = bounds[0];
        b.GetAttribute("data-group-id").Should().Be("g1",
            "F6 — the rect carries its group id for tests / export tooling");

        // 8 px pad in Razor (const pad = 8): (100 - 8, 100 - 8, 320 + 16, 200 + 16).
        b.GetAttribute("x").Should().Be("92");
        b.GetAttribute("y").Should().Be("92");
        b.GetAttribute("width").Should().Be("336");
        b.GetAttribute("height").Should().Be("216");
    }

    [Fact]
    public void GroupBounds_AreNotRenderedInOverlayOrDecoratorPane()
    {
        // F6 decision — group bounds are a model concern (bg-pane), not a
        // selection/decorator concern. Any leak into overlay/decorator would
        // suggest the pre-F6 imperative JS path has resurfaced.
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildGroupedDocument())
            .Add(c => c.ShowPageView, false));

        cut.Find(".tm-diagram-overlay-pane").QuerySelectorAll(".tm-diagram-group-bounds").Should().BeEmpty(
            "F6 removed the JS _renderGroupBounds path; bounds no longer live in overlay-pane");
        cut.Find(".tm-diagram-decorator-pane").QuerySelectorAll(".tm-diagram-group-bounds").Should().BeEmpty(
            "decorator-pane is reserved for handles, not bounds");
    }

    [Fact]
    public void GroupBounds_RecomputeWhenNodePositionChanges_SimulatingDragCommit()
    {
        // F6.5 smoke (bUnit variant): after a group member's coordinates change
        // and Blazor re-renders, the bounds rect follows. This is the same path
        // the UI uses when a drag commit updates Document.Nodes[i].X/Y.
        var doc = BuildGroupedDocument();
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var before = cut.Find(".tm-diagram-bg-pane rect.tm-diagram-group-bounds");
        before.GetAttribute("width").Should().Be("336");
        before.GetAttribute("height").Should().Be("216");

        // Move the second group member further down-right; bounds must grow.
        doc.Nodes[1].X = 500;
        doc.Nodes[1].Y = 400;
        cut.Render(p => p.Add(c => c.Document, doc));

        var after = cut.Find(".tm-diagram-bg-pane rect.tm-diagram-group-bounds");
        // New extent: (100,100)–(620,460) with ±8 pad → (92,92, 536, 376).
        after.GetAttribute("x").Should().Be("92");
        after.GetAttribute("y").Should().Be("92");
        after.GetAttribute("width").Should().Be("536");
        after.GetAttribute("height").Should().Be("376");
    }

    [Fact]
    public void UngroupedDocument_EmitsNoGroupBoundsRect()
    {
        // No nodes have GroupId in the baseline sample, so the Razor @if that
        // selects grouped nodes emits nothing — bg-pane must contain zero
        // .tm-diagram-group-bounds rects.
        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, BuildSampleDocument())
            .Add(c => c.ShowPageView, false));

        cut.FindAll("rect.tm-diagram-group-bounds").Should().BeEmpty(
            "F6 — no groups in Document ⇒ no bounds rects anywhere in the canvas");
    }
}
