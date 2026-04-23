using System.Globalization;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

/// <summary>
/// Locks down the Razor-produced coordinate-system scaffolding that
/// <c>diagram-editor.js</c> relies on: the SVG <c>viewBox</c> attribute (now the
/// sole source of zoom after F2) and the SVG <c>transform</c> attribute that
/// <c>_nodeRect</c>/<c>_getNodeRotation</c> parse for each node after F3.
///
/// <para>
/// Related refactor: planning/DIAGRAM_UNIFIED_SVG_PLAN.md — F0.3 / F0.6 / F2.5 / F3.
/// </para>
/// </summary>
public class TmDiagramCanvasCoordinateTests : LocalizationTestBase
{
    public TmDiagramCanvasCoordinateTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void ViewBox_AtUnitScale_IsIdentity()
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = 1.0;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        svg.GetAttribute("viewBox").Should().Be("0 0 800 600",
            "at Page.Scale = 1 the viewBox is the identity mapping used by _screenToDoc");

        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().NotContain("transform:scale(",
            "F2.5 removed the duplicate CSS scale transform");
    }

    [Fact]
    public void ViewBox_AtScale075_IsFoldedIntoDimensions()
    {
        // Before F2 the same 0.75 zoom lived in two places — CSS transform:scale
        // on <svg> plus JS-driven CSS on the HTML overlay — producing the drift
        // bug documented in _findNearestPortOnNode. F2 folds Page.Scale directly
        // into the viewBox: (W/0.75, H/0.75) at origin (0, 0).
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = 0.75;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().NotContain("transform:scale(",
            "no CSS scale — viewBox is the single source of zoom now");

        var vb = svg.GetAttribute("viewBox");
        vb.Should().Be("0 0 1066.67 800",
            "Document.Width/scale = 800/0.75 ≈ 1066.67, Document.Height/scale = 600/0.75 = 800");
    }

    [Fact]
    public void NodePosition_IsEncodedInSvgTransform_AfterF3()
    {
        // Post-F3 _nodeRect() parses this exact substring from the <g transform>
        // attribute (no more CSS transforms on node divs).
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 123.5,
            Y = 45.25,
            W = 120,
            H = 60
        };
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var nodeEl = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        var transform = nodeEl.GetAttribute("transform") ?? string.Empty;

        var expectedX = node.X.ToString("0.##", CultureInfo.InvariantCulture);
        var expectedY = node.Y.ToString("0.##", CultureInfo.InvariantCulture);

        transform.Should().Contain($"translate({expectedX},{expectedY})",
            "_nodeRect() parses this exact substring — any change breaks JS drag math");
    }

    [Fact]
    public void RotatedNode_EncodesRotationInSvgTransform_AfterF3()
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60,
            Rotation = 45
        };
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var nodeEl = cut.Find($"g.tm-diagram-node[data-node-id='{node.Id}']");
        var transform = nodeEl.GetAttribute("transform") ?? string.Empty;

        transform.Should().Contain("rotate(45 60 30)",
            "F3 encodes rotation around the node centre (W/2, H/2) directly on the <g> transform attribute");
    }

    [Theory]
    [InlineData(0.5, "0 0 1600 1200")]
    [InlineData(1.0, "0 0 800 600")]
    [InlineData(1.5, "0 0 533.33 400")]
    [InlineData(2.0, "0 0 400 300")]
    public void ViewBox_TracksDocumentScale(double scale, string expectedViewBox)
    {
        // Post-F2 Page.Scale is folded into the viewBox dimensions: larger
        // scale ⇒ smaller viewBox ⇒ content appears larger on screen.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = scale;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        svg.GetAttribute("viewBox").Should().Be(expectedViewBox);

        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().NotContain("transform:scale(",
            "CSS transform scale is permanently gone — viewBox owns the zoom");
    }
}
