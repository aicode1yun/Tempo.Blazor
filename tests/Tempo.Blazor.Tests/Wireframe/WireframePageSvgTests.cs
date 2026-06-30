using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Faze T (T1) tests for <see cref="WireframePageSvg.BuildFragment"/>: the shared, interaction-free
/// page → SVG presentation layer. Verifies it renders background + connectors + elements and that it
/// contains NONE of the editor's interaction chrome (selection/resize handles, connector hit-test
/// paths, waypoint handles, pointer-events, cursor styling).
/// </summary>
public class WireframePageSvgTests : Bunit.TestContext
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>A definition that draws a uniquely identifiable rect, so we can assert it rendered.</summary>
    private static WireframeComponentDef KnownDef(string type) => new()
    {
        Type        = type,
        DisplayName = type,
        Category    = "Test",
        IsBuiltIn   = true,
        RenderSvg   = (el, b) =>
        {
            b.OpenElement(0, "rect");
            b.AddAttribute(1, "width", el.W);
            b.AddAttribute(2, "height", el.H);
            b.AddAttribute(3, "data-known-render", type);
            b.CloseElement();
        }
    };

    private static (WireframeComponentRegistry Registry, WireframePage Page) BuildScene()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(KnownDef("TmButton"));

        var page = new WireframePage { Name = "P1", Width = 800, Height = 600 };
        page.Elements.Add(new WireframeElement { Id = "el-a", Type = "TmButton", X = 40, Y = 40, W = 120, H = 36 });
        page.Elements.Add(new WireframeElement { Id = "el-b", Type = "TmButton", X = 400, Y = 300, W = 120, H = 36 });
        page.Connectors.Add(new WireframeConnector
        {
            Id = "c1", FromId = "el-a", ToId = "el-b", Label = "EdgeLbl", EndArrow = "classic",
        });
        return (registry, page);
    }

    // ── Content ────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFragment_RendersBackgroundConnectorsAndElements()
    {
        var (registry, page) = BuildScene();

        var cut = Render(WireframePageSvg.BuildFragment(page, registry));
        var markup = cut.Markup;

        cut.Find("svg[viewBox]").Should().NotBeNull();                 // page root svg
        markup.Should().Contain("fill=\"white\"");                    // background rect
        markup.Should().Contain("data-known-render=\"TmButton\"");    // elements via def.RenderSvg
        markup.Should().Contain("<path");                             // connector visible path
        markup.Should().Contain("marker-end");                       // arrowhead reference
        markup.Should().Contain("<marker");                          // arrow marker def in <defs>
        markup.Should().Contain("EdgeLbl");                          // connector label
    }

    [Fact]
    public void BuildFragment_RendersEveryElement()
    {
        var (registry, page) = BuildScene();

        var cut = Render(WireframePageSvg.BuildFragment(page, registry));

        cut.FindAll("g[data-el-id]").Count.Should().Be(2);
    }

    // ── No interaction chrome (the core T1 contract) ────────────────────────────

    [Fact]
    public void BuildFragment_OmitsAllInteractionChrome()
    {
        var (registry, page) = BuildScene();

        var markup = Render(WireframePageSvg.BuildFragment(page, registry)).Markup;

        markup.Should().NotContain("tm-wd-connector__hit");      // no connector hit-test path
        markup.Should().NotContain("tm-wd-connector__waypoint"); // no waypoint handles
        markup.Should().NotContain("data-waypoint-index");
        markup.Should().NotContain("pointer-events");            // no interaction surfaces
        markup.Should().NotContain("cursor:");                   // no cursor styling
        markup.Should().NotContain("stroke=\"transparent\"");    // hit paths are transparent/wide
    }

    // ── Robustness ──────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFragment_UnknownComponent_RendersDashedFallbackWithoutCrashing()
    {
        var registry = new WireframeComponentRegistry(); // empty registry → nothing resolves
        var page = new WireframePage { Name = "P", Width = 400, Height = 300 };
        page.Elements.Add(new WireframeElement { Id = "x", Type = "TotallyUnknown", X = 10, Y = 10, W = 100, H = 40 });

        var markup = Render(WireframePageSvg.BuildFragment(page, registry)).Markup;

        markup.Should().Contain("stroke-dasharray");   // dashed placeholder box
        markup.Should().Contain("TotallyUnknown");      // shows the missing component type
    }

    [Fact]
    public void BuildFragment_AppScopedCustomComponent_ResolvesAndRenders()
    {
        var registry = new WireframeComponentRegistry();
        var scope = WireframeComponentScope.ForApp("app-1");
        registry.RegisterDefinition(
            new WireframeComponentDef
            {
                Type = "Card",
                DisplayName = "Card",
                Category = "Custom",
                RenderSvg = (el, b) =>
                {
                    b.OpenElement(0, "rect");
                    b.AddAttribute(1, "data-custom", "yes");
                    b.CloseElement();
                },
            },
            scope.AppId);

        var page = new WireframePage { Name = "P", Width = 400, Height = 300 };
        page.Elements.Add(new WireframeElement { Id = "c", Type = scope.NamespaceType("Card"), X = 10, Y = 10, W = 100, H = 60 });

        var markup = Render(WireframePageSvg.BuildFragment(page, registry, scope)).Markup;

        markup.Should().Contain("data-custom=\"yes\"");
    }

    [Fact]
    public void BuildFragment_DroppedConnector_WhenEndpointMissing()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(KnownDef("TmButton"));

        var page = new WireframePage { Name = "P", Width = 400, Height = 300 };
        page.Elements.Add(new WireframeElement { Id = "only", Type = "TmButton", X = 10, Y = 10, W = 80, H = 30 });
        // connector references a non-existent target → must be skipped, not throw
        page.Connectors.Add(new WireframeConnector { Id = "c", FromId = "only", ToId = "ghost" });

        var markup = Render(WireframePageSvg.BuildFragment(page, registry)).Markup;

        markup.Should().NotContain("<path");           // no connector path emitted
        markup.Should().Contain("data-known-render");   // element still renders
    }
}
