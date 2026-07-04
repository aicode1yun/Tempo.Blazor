using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the implementation brief engine and tool.</summary>
public class WireframeImplementationBriefTests
{
    private static string KnownType() => new WireframeSchemaRegistry([new BuiltInComponentSchemas()]).GetAll().First().Type;
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static WireframeDocument OrdersDoc()
    {
        var doc = new WireframeDocument { Title = "Orders" };
        doc.EnsureActivePage();
        var page = doc.ActivePage!;
        page.Width = 1000;
        page.Height = 1000;
        // header (top band)
        page.Elements.Add(new WireframeElement { Id = "hd", Type = KnownType(), X = 0, Y = 0, W = 1000, H = 80 });
        // sidebar (left column)
        page.Elements.Add(new WireframeElement { Id = "sb", Type = KnownType(), X = 0, Y = 200, W = 150, H = 600 });
        // content
        page.Elements.Add(new WireframeElement { Id = "tbl", Type = KnownType(), X = 200, Y = 200, W = 700, H = 400 });
        page.Elements.Add(new WireframeElement { Id = "btn", Type = KnownType(), X = 200, Y = 650, W = 100, H = 36 });
        // footer (bottom band)
        page.Elements.Add(new WireframeElement { Id = "ft", Type = KnownType(), X = 0, Y = 950, W = 1000, H = 40 });
        // flow: table row → detail button
        page.Connectors.Add(new WireframeConnector { FromId = "tbl", ToId = "btn", Label = "open detail" });
        return doc;
    }

    [Fact]
    public void Classify_AssignsRegionsByGeometry()
    {
        WireframeImplementationBrief.Classify(new WireframeElement { Y = 0, H = 80 }, 1000, 1000).Should().Be("header");
        WireframeImplementationBrief.Classify(new WireframeElement { Y = 960, H = 30 }, 1000, 1000).Should().Be("footer");
        WireframeImplementationBrief.Classify(new WireframeElement { X = 0, Y = 300, W = 150, H = 400 }, 1000, 1000).Should().Be("sidebar");
        WireframeImplementationBrief.Classify(new WireframeElement { X = 300, Y = 300, W = 400, H = 200 }, 1000, 1000).Should().Be("content");
    }

    [Fact]
    public void Build_ProducesRegionsComponentsAndFlows()
    {
        var brief = WireframeImplementationBrief.Build(OrdersDoc());

        brief.Title.Should().Be("Orders");
        brief.Pages.Should().ContainSingle();
        var page = brief.Pages[0];
        page.Regions.Select(r => r.Kind).Should().ContainInOrder("header", "sidebar", "content", "footer");
        page.Components.Single().Count.Should().Be(5);
        page.Flows.Should().ContainSingle();
        page.Flows[0].Label.Should().Be("open detail");
        brief.ComponentsUsed.Single().Count.Should().Be(5);
    }

    [Fact]
    public void Build_ReportsRoleBesideConcreteType()
    {
        var doc = new WireframeDocument { Title = "Login" };
        doc.EnsureActivePage();
        var page = doc.ActivePage!;
        page.Elements.Add(new WireframeElement
        {
            Id = "otp",
            Type = "TmMaskedTextBox",
            Role = "otp-input",
            X = 24,
            Y = 120,
            W = 180,
            H = 36
        });

        var brief = WireframeImplementationBrief.Build(doc);

        var element = brief.Pages[0].Regions.Single().Elements.Single();
        element.Type.Should().Be("TmMaskedTextBox");
        element.Role.Should().Be("otp-input");
        brief.Pages[0].Components.Single().Role.Should().Be("otp-input");
        brief.ComponentsUsed.Single().Role.Should().Be("otp-input");
    }

    [Fact]
    public async Task BriefTool_ReturnsBriefForStoredDocument()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Orders", "/", OrdersDoc());

        var root = Parse(await WireframeBriefTools.GetImplementationBrief(backend, backend, id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("title").GetString().Should().Be("Orders");
        root.GetProperty("pages").GetArrayLength().Should().Be(1);
        root.GetProperty("componentsUsed").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task BriefTool_Unknown_ReturnsNotFound()
    {
        var backend = new FakeWireframeBackend();

        var root = Parse(await WireframeBriefTools.GetImplementationBrief(backend, backend, Guid.NewGuid()));

        root.GetProperty("error").GetString().Should().Be("not_found");
    }
}
