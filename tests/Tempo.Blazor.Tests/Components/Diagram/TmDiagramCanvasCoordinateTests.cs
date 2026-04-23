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
/// Safety-net tests that lock down the Razor-produced inputs into the JS coordinate system
/// (viewBox, inline transforms, node position). They do not execute JS — they verify that the
/// scaffolding the JS relies on is stable. After F2/F3 the assertions about CSS transforms
/// disappear and are replaced by SVG-attribute-based checks.
///
/// Related refactor: planning/DIAGRAM_UNIFIED_SVG_PLAN.md (F0.3, F0.6).
/// </summary>
public class TmDiagramCanvasCoordinateTests : LocalizationTestBase
{
    public TmDiagramCanvasCoordinateTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void ScreenToDoc_RoundTrip_AtUnitScale_IdentityMapping()
    {
        // At scale 1 and the default viewBox covering the full document, a screen-space
        // point on the canvas maps 1:1 to document coordinates.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = 1.0;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        svg.GetAttribute("viewBox").Should().Be("0 0 800 600",
            "a 1:1 viewBox at unit scale is the identity CTM used by _screenToDoc");

        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().Contain("transform:scale(1)",
            "the inline CSS scale at 1 is the neutral value that F2.5 will remove entirely");
    }

    [Fact]
    public void ScreenToDoc_RoundTrip_AtScale075_RenderesMatchingScaleAttribute()
    {
        // Today the same 0.75 scale is applied TWICE: once as CSS on <svg> (via style) and
        // again by JS onto the HTML overlay's transform-layer. The drift bug in
        // _findNearestPortOnNode arises exactly because these two sources can fall out of
        // sync. F1/F2 will reduce this to a single viewBox-driven scale.
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = 0.75;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        var style = svg.GetAttribute("style") ?? string.Empty;
        style.Should().Contain("transform:scale(0.75)",
            "Razor renders the CSS scale from Document.ActivePage.Scale; drift-bug root cause");

        svg.GetAttribute("viewBox").Should().Be("0 0 800 600",
            "viewBox stays at document dimensions — zoom is applied by the duplicate CSS transform today");
    }

    [Fact]
    public void NodePosition_IsEncodedInCssTranslate_Baseline()
    {
        // Documents the exact string format that diagram-editor.js::_nodeRect parses today.
        // When F3 replaces this with SVG <g transform> attributes, this test will be rewritten
        // to assert the transform attribute instead.
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

        var nodeEl = cut.Find($"[data-node-id='{node.Id}']");
        var style = nodeEl.GetAttribute("style") ?? string.Empty;

        var expectedX = node.X.ToString("0.##", CultureInfo.InvariantCulture);
        var expectedY = node.Y.ToString("0.##", CultureInfo.InvariantCulture);

        style.Should().Contain($"translate({expectedX}px, {expectedY}px)",
            "_nodeRect() parses this exact substring — any change breaks JS-side drag math");
    }

    [Fact]
    public void RotatedNode_EncodesRotationInCssTransform_Baseline()
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

        var nodeEl = cut.Find($"[data-node-id='{node.Id}']");
        var style = nodeEl.GetAttribute("style") ?? string.Empty;

        style.Should().Contain("rotate(45deg)",
            "_getNodeRotation() parses this exact substring; F5 will move rotation onto the SVG <g> wrapping the node");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void SvgScaleTransform_TracksDocumentScale(double scale)
    {
        var doc = new DiagramDocument { Width = 800, Height = 600 };
        doc.ActivePage!.Scale = scale;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ShowPageView, false));

        var svg = cut.Find("svg");
        var style = svg.GetAttribute("style") ?? string.Empty;

        var expected = $"transform:scale({scale.ToString("0.##", CultureInfo.InvariantCulture)})";
        style.Should().Contain(expected,
            $"document scale {scale} must render verbatim into the inline CSS transform today");
    }
}
