using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// bUnit tests for TmWireframeDesignerCanvas.
/// JS interop is handled by bUnit's Loose mock (all JS calls are no-ops).
/// </summary>
public class TmWireframeDesignerCanvasTests : LocalizationTestBase
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static WireframeComponentDef MakeDef(string type)
        => new()
        {
            Type        = type,
            DisplayName = type,
            Category    = "Test",
            DefaultWidth  = 120,
            DefaultHeight = 36,
            IsBuiltIn   = true,
            Props       = [],
            RenderSvg   = (el, builder) =>
            {
                builder.OpenElement(0, "rect");
                builder.AddAttribute(1, "width", el.W);
                builder.AddAttribute(2, "height", el.H);
                builder.AddAttribute(3, "fill", "#eee");
                builder.CloseElement();
            }
        };

    private WireframeComponentRegistry BuildRegistry(params string[] types)
    {
        var registry = new WireframeComponentRegistry();
        foreach (var t in types)
            registry.RegisterDefinition(MakeDef(t));
        return registry;
    }

    private static WireframeDocument DocWith(params (string type, double x, double y)[] elements)
    {
        var doc = new WireframeDocument { Title = "Test", Width = 800, Height = 600 };
        foreach (var (type, x, y) in elements)
            doc.Elements.Add(WireframeDocumentExtensions.NewElement(type, x, y));
        return doc;
    }

    // ── Container renders ──────────────────────────────────────────────────────

    [Fact]
    public void Canvas_RendersWrapper()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var cut = RenderComponent<TmWireframeDesignerCanvas>();

        cut.Find(".tm-wd-canvas-wrap").Should().NotBeNull();
    }

    [Fact]
    public void Canvas_RendersSvgElement()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var cut = RenderComponent<TmWireframeDesignerCanvas>();

        cut.Find("svg.tm-wd-canvas__svg").Should().NotBeNull();
    }

    [Fact]
    public void Canvas_SvgHasAriaLabel()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var cut = RenderComponent<TmWireframeDesignerCanvas>();

        cut.Find("svg").GetAttribute("aria-label").Should().Be("Wireframe canvas");
    }

    // ── Grid rendering ─────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_ShowGrid_True_RendersGridPatterns()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.ShowGrid, true));

        // Grid is rendered inside <defs> as <pattern> elements
        cut.FindAll("pattern").Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Canvas_ShowGrid_False_NoGridPatterns()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.ShowGrid, false));

        cut.FindAll("pattern").Should().BeEmpty();
    }

    // ── Element rendering ──────────────────────────────────────────────────────

    [Fact]
    public void Canvas_RendersElementsFromDocument()
    {
        var registry = BuildRegistry("TmButton", "TmCard");
        Services.AddSingleton(registry);

        var doc = DocWith(("TmButton", 0, 0), ("TmCard", 100, 50));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc));

        cut.FindAll("g[data-el-id]").Should().HaveCount(2);
    }

    [Fact]
    public void Canvas_ElementGroupHasDataTypeAttribute()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var doc = DocWith(("TmButton", 10, 20));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc));

        var g = cut.Find("g[data-el-id]");
        g.GetAttribute("data-type").Should().Be("TmButton");
    }

    [Fact]
    public void Canvas_ElementGroupHasTransform()
    {
        Services.AddSingleton(BuildRegistry("TmButton"));

        var doc = DocWith(("TmButton", 10, 20));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc));

        var g = cut.Find("g[data-el-id]");
        g.GetAttribute("transform").Should().Contain("translate(10, 20)");
    }

    [Fact]
    public void Canvas_ElementsOrderedByZIndex()
    {
        Services.AddSingleton(BuildRegistry("TmButton", "TmCard"));

        var doc = new WireframeDocument { Title = "Test", Width = 800, Height = 600 };
        var el1 = WireframeDocumentExtensions.NewElement("TmCard", 0, 0);
        el1.ZIndex = 5;
        var el2 = WireframeDocumentExtensions.NewElement("TmButton", 0, 0);
        el2.ZIndex = 1;
        doc.Elements.AddRange([el1, el2]);

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc));

        var groups = cut.FindAll("g[data-el-id]");
        groups[0].GetAttribute("data-type").Should().Be("TmButton"); // lower ZIndex first
        groups[1].GetAttribute("data-type").Should().Be("TmCard");
    }

    [Fact]
    public void Canvas_NullDocument_RendersWithoutElements()
    {
        Services.AddSingleton(BuildRegistry());

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, (WireframeDocument?)null));

        cut.FindAll("g[data-el-id]").Should().BeEmpty();
    }

    // ── Unknown type fallback ──────────────────────────────────────────────────

    [Fact]
    public void Canvas_UnknownComponentType_RendersFallbackGroup()
    {
        // Registry has no "UnknownWidget" — canvas should still render a group (fallback rect)
        Services.AddSingleton(BuildRegistry());

        var doc = DocWith(("UnknownWidget", 0, 0));

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc));

        // A group is rendered even for unknown types (fallback shape)
        cut.FindAll("g[data-el-id]").Should().HaveCount(1);
    }

    // ── ReadOnly parameter ─────────────────────────────────────────────────────

    [Fact]
    public void Canvas_ReadOnly_True_RendersWithoutError()
    {
        // Smoke test: ReadOnly=true should render without throwing
        Services.AddSingleton(BuildRegistry("TmButton"));

        var doc = DocWith(("TmButton", 0, 0));

        var act = () => RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc)
            .Add(x => x.ReadOnly, true));

        act.Should().NotThrow();
    }

    // ── Class parameter ────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_ClassParameter_AppliedToWrapper()
    {
        Services.AddSingleton(BuildRegistry());

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Class, "extra-class"));

        cut.Find(".tm-wd-canvas-wrap").ClassList.Should().Contain("extra-class");
    }

    // ── Document dimensions ────────────────────────────────────────────────────

    [Fact]
    public void Canvas_BackgroundRect_UsesDocumentDimensions()
    {
        // Disable grid so there are no pattern rects interfering — the first rect is the canvas bg
        Services.AddSingleton(BuildRegistry());

        var doc = new WireframeDocument { Width = 1440, Height = 900 };

        var cut = RenderComponent<TmWireframeDesignerCanvas>(p => p
            .Add(x => x.Document, doc)
            .Add(x => x.ShowGrid, false));

        // With grid off the first rect is the white canvas background
        var rects = cut.FindAll("rect");
        rects[0].GetAttribute("width").Should().Be("1440");
        rects[0].GetAttribute("height").Should().Be("900");
    }
}
