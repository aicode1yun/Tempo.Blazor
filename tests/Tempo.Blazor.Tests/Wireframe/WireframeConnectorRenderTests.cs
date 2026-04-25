using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// bUnit render tests for connector SVG output in <see cref="TmWireframeDesignerCanvas"/>.
/// </summary>
public class WireframeConnectorRenderTests : LocalizationTestBase
{
    public WireframeConnectorRenderTests()
    {
        Services.AddSingleton(new WireframeComponentRegistry());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Canvas_WithNoConnectors_DoesNotRenderConnectorGroup()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        cut.FindAll("[data-connector-id]").Should().BeEmpty();
    }

    [Fact]
    public void Canvas_WithConnector_RendersConnectorPath()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        doc.Connectors.Add(new WireframeConnector { FromId = "e1", ToId = "e2", Routing = "straight" });

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        var groups = cut.FindAll("[data-connector-id]");
        groups.Should().HaveCount(1);

        var path = groups[0].QuerySelector(".tm-wd-connector__path");
        path.Should().NotBeNull();
        path!.GetAttribute("d").Should().StartWith("M");
    }

    [Fact]
    public void Canvas_WithConnector_RendersHitTestPath()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        doc.Connectors.Add(new WireframeConnector { FromId = "e1", ToId = "e2" });

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        var hit = cut.Find(".tm-wd-connector__hit");
        hit.Should().NotBeNull();
        hit.GetAttribute("stroke-width").Should().Be("12");
    }

    [Fact]
    public void Canvas_WithEndArrow_RendersMarkerInDefs()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        var conn = new WireframeConnector { FromId = "e1", ToId = "e2", EndArrow = "classic" };
        doc.Connectors.Add(conn);

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        var defs = cut.Find("defs");
        defs.InnerHtml.Should().Contain("<marker");
        defs.InnerHtml.Should().Contain($"tm-wd-arrow-end-classic-{conn.Id}");
    }

    [Fact]
    public void Canvas_WithConnectorLabel_RendersText()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        doc.Connectors.Add(new WireframeConnector { FromId = "e1", ToId = "e2", Label = "Click me" });

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        var text = cut.Find(".tm-wd-connector__label");
        text.TextContent.Should().Be("Click me");
    }

    [Fact]
    public async Task Canvas_SelectedConnector_HasSelectedClass()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        var conn = new WireframeConnector { FromId = "e1", ToId = "e2" };
        doc.Connectors.Add(conn);

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        // Simulate selection by invoking the JS callback
        await cut.InvokeAsync(() => cut.Instance.JsOnConnectorSelectionChanged([conn.Id]));
        cut.Render();

        var groups = cut.FindAll(".tm-wd-connector--selected");
        groups.Should().HaveCount(1);
        groups[0].GetAttribute("data-connector-id").Should().Be(conn.Id);
    }

    [Fact]
    public void Canvas_MultipleConnectors_RendersAll()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", Type = "TmButton", X = 0, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e2", Type = "TmButton", X = 200, Y = 0, W = 100, H = 40 });
        doc.Elements.Add(new WireframeElement { Id = "e3", Type = "TmButton", X = 400, Y = 0, W = 100, H = 40 });
        doc.Connectors.Add(new WireframeConnector { FromId = "e1", ToId = "e2" });
        doc.Connectors.Add(new WireframeConnector { FromId = "e2", ToId = "e3" });

        var cut = RenderComponent<TmWireframeDesignerCanvas>(parameters =>
            parameters.Add(p => p.Document, doc));

        cut.FindAll("[data-connector-id]").Should().HaveCount(2);
    }
}
