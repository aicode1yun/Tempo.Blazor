using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Faze T (T2) tests for <see cref="WireframeSvgRenderer"/>: headless server-side rendering of
/// wireframe pages/documents via <see cref="Microsoft.AspNetCore.Components.Web.HtmlRenderer"/>.
/// Covers single page, full document (order preserved), empty page, unknown component, app scope,
/// determinism, and sanitizer-friendliness (no script/JS).
/// </summary>
public class WireframeSvgRendererTests
{
    // ── Setup ──────────────────────────────────────────────────────────────────

    private static WireframeSvgRenderer BuildRenderer(WireframeComponentRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new WireframeSvgRenderer(registry, services.BuildServiceProvider());
    }

    private static WireframeComponentRegistry BuiltInRegistry()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());
        return registry;
    }

    private static WireframePage PageWith(string name, double w, double h, params string[] elementTypes)
    {
        var page = new WireframePage { Name = name, Width = w, Height = h };
        var x = 20;
        foreach (var type in elementTypes)
        {
            page.Elements.Add(new WireframeElement { Type = type, X = x, Y = 20, W = 120, H = 36 });
            x += 140;
        }
        return page;
    }

    // ── Single page ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPageAsync_ReturnsValidSanitizableSvg()
    {
        var renderer = BuildRenderer(BuiltInRegistry());
        var page = PageWith("Login", 800, 600, "TmButton", "TmTextInput", "TmCard");

        var svg = await renderer.RenderPageAsync(page);

        svg.Should().StartWith("<svg");
        svg.Should().Contain("<rect");

        // Sanitizer alignment: nothing executable in the output.
        svg.Should().NotContainEquivalentOf("<script");
        svg.Should().NotContainEquivalentOf("javascript:");
        svg.Should().NotContainEquivalentOf("onload=");
        svg.Should().NotContainEquivalentOf("onclick=");
        svg.Should().NotContainEquivalentOf("onerror=");
        svg.Should().NotContainEquivalentOf("<foreignObject");
    }

    // ── Whole document ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderDocumentAsync_ReturnsEveryPage_InDocumentOrder()
    {
        var renderer = BuildRenderer(BuiltInRegistry());
        var doc = new WireframeDocument { Title = "Multi" };
        doc.Pages.Clear();
        doc.Pages.Add(PageWith("Home", 800, 600, "TmButton"));
        doc.Pages.Add(PageWith("Details", 1024, 768, "TmCard"));
        doc.Pages.Add(PageWith("Settings", 640, 480, "TmTextInput"));

        var rendered = await renderer.RenderDocumentAsync(doc);

        rendered.Should().HaveCount(3);
        rendered.Select(r => r.Name).Should().ContainInOrder("Home", "Details", "Settings");
        rendered[1].Width.Should().Be(1024);
        rendered[1].Height.Should().Be(768);
        rendered.Should().OnlyContain(r => r.Svg.StartsWith("<svg"));
        rendered[1].Svg.Should().Contain("viewBox=\"0 0 1024 768\"");
    }

    // ── Empty page ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPageAsync_EmptyPage_ReturnsSizedSvgWithName()
    {
        var renderer = BuildRenderer(BuiltInRegistry());
        var page = new WireframePage { Name = "Blank Screen", Width = 500, Height = 400 }; // no elements

        var svg = await renderer.RenderPageAsync(page);

        svg.Should().NotBeNullOrEmpty();
        svg.Should().StartWith("<svg");
        svg.Should().Contain("viewBox=\"0 0 500 400\"");
        svg.Should().Contain("data-page-name=\"Blank Screen\"");
        svg.Should().Contain("Blank Screen");   // visible placeholder so the preview is never a blank box
    }

    // ── Unknown component ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderPageAsync_UnknownComponent_RendersFallback_NoThrow()
    {
        var renderer = BuildRenderer(new WireframeComponentRegistry()); // empty registry
        var page = new WireframePage { Name = "P", Width = 400, Height = 300 };
        page.Elements.Add(new WireframeElement { Type = "GhostWidget", X = 10, Y = 10, W = 100, H = 40 });

        var svg = await renderer.RenderPageAsync(page);

        svg.Should().Contain("stroke-dasharray");   // dashed placeholder box
        svg.Should().Contain("GhostWidget");         // the missing type is shown
    }

    // ── App-scoped custom component ──────────────────────────────────────────────

    [Fact]
    public async Task RenderPageAsync_AppScopedCustomComponent_RendersViaScope()
    {
        var registry = new WireframeComponentRegistry();
        var scope = WireframeComponentScope.ForApp("app-77");
        registry.RegisterDefinition(
            new WireframeComponentDef
            {
                Type = "ProductTile",
                DisplayName = "Product Tile",
                Category = "Custom",
                RenderSvg = (el, b) =>
                {
                    b.OpenElement(0, "rect");
                    b.AddAttribute(1, "data-product-tile", "rendered");
                    b.CloseElement();
                },
            },
            scope.AppId);

        var renderer = BuildRenderer(registry);
        var page = new WireframePage { Name = "Catalog", Width = 600, Height = 400 };
        page.Elements.Add(new WireframeElement { Type = scope.NamespaceType("ProductTile"), X = 20, Y = 20, W = 160, H = 120 });

        var svg = await renderer.RenderPageAsync(page, scope);

        svg.Should().Contain("data-product-tile=\"rendered\"");
    }

    // ── Determinism ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderDocumentAsync_IsDeterministic_ForSameInput()
    {
        var renderer = BuildRenderer(BuiltInRegistry());
        var doc = new WireframeDocument { Title = "Det" };
        doc.Pages.Clear();
        var page = PageWith("One", 800, 600, "TmButton", "TmCard");
        page.Elements[0].Id = "fixed-a";
        page.Elements[1].Id = "fixed-b";
        doc.Pages.Add(page);

        var first = await renderer.RenderDocumentAsync(doc);
        var second = await renderer.RenderDocumentAsync(doc);

        first.Select(r => r.Svg).Should().Equal(second.Select(r => r.Svg));
    }
}
