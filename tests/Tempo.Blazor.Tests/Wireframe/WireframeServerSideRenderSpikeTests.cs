using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Faze T (T0) spike. Proves that a wireframe page can be rendered to an SVG string
/// fully headless — no browser, no JS interop — by reusing the existing
/// <see cref="WireframeComponentDef.RenderSvg"/> definitions through the static
/// <see cref="HtmlRenderer"/>.
///
/// This is the exact production mechanism the future <c>IWireframeSvgRenderer</c> (T2)
/// will use, so the spike deliberately exercises <see cref="HtmlRenderer"/> and the real
/// component registry rather than bUnit. It must hold for:
///   1. built-in components,
///   2. every page of a multi-page document (the original blocker — the live
///      <c>ExportSvgAsync</c> can only export the active page),
///   3. app-scoped custom components registered via <see cref="WireframeComponentScope"/>.
///
/// The <see cref="BuildPageFragment"/> helper is a minimal, interaction-free mirror of what
/// T1's <c>WireframePageSvg.BuildFragment</c> will produce.
/// </summary>
public class WireframeServerSideRenderSpikeTests
{
    // ── T0.1 — built-in components render to valid SVG, headless ────────────────

    [Fact]
    public async Task HtmlRenderer_RendersBuiltInElements_ToValidSvg()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());

        var page = new WireframePage { Name = "Login", Width = 800, Height = 600 };
        page.Elements.Add(new WireframeElement { Type = "TmButton", X = 40, Y = 40, W = 120, H = 36, ZIndex = 0 });
        page.Elements.Add(new WireframeElement { Type = "TmTextInput", X = 40, Y = 100, W = 200, H = 36, ZIndex = 1 });

        var svg = await RenderToSvgAsync(BuildPageFragment(page, registry, scope: null));

        svg.Should().StartWith("<svg");
        svg.Should().Contain("data-el-id");        // element groups were emitted
        svg.Should().Contain("<rect");             // def.RenderSvg actually drew shapes
        svg.Should().Contain("Button");            // TmButton default label proves the lambda ran
    }

    // ── T0.2 — every page of a multi-page document renders independently ────────

    [Fact]
    public async Task HtmlRenderer_RendersEveryPage_Independently()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());

        var home = new WireframePage { Name = "Home", Width = 800, Height = 600 };
        home.Elements.Add(new WireframeElement { Type = "TmButton", X = 10, Y = 10, W = 120, H = 36 });

        var details = new WireframePage { Name = "Details", Width = 1024, Height = 768 };
        details.Elements.Add(new WireframeElement { Type = "TmCard", X = 20, Y = 20, W = 240, H = 160 });

        WireframePage[] pages = [home, details];

        var svgs = new List<string>();
        foreach (var page in pages)
            svgs.Add(await RenderToSvgAsync(BuildPageFragment(page, registry, scope: null)));

        svgs.Should().HaveCount(2);
        svgs.Should().OnlyContain(s => s.StartsWith("<svg") && s.Contains("<rect"));
        svgs[0].Should().Contain("Button");        // page 1 = its own content
        svgs[1].Should().Contain("viewBox=\"0 0 1024 768\"");   // page 2 = its own dimensions
    }

    // ── T0.3 — app-scoped custom components resolve and render server-side ──────

    [Fact]
    public async Task HtmlRenderer_RendersAppScopedCustomComponent()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());

        var scope = WireframeComponentScope.ForApp("spike-app");
        registry.RegisterDefinition(
            new WireframeComponentDef
            {
                Type = "MyProductCard",
                Category = "Custom",
                DisplayName = "My Product Card",
                RenderSvg = (el, b) =>
                {
                    b.OpenElement(0, "rect");
                    b.AddAttribute(1, "width", Fmt(el.W));
                    b.AddAttribute(2, "height", Fmt(el.H));
                    b.AddAttribute(3, "data-custom-marker", "spike-custom-marker");
                    b.CloseElement();
                },
            },
            scope.AppId);

        var scopedType = scope.NamespaceType("MyProductCard");   // app:spike-app:MyProductCard
        var page = new WireframePage { Name = "Custom", Width = 600, Height = 400 };
        page.Elements.Add(new WireframeElement { Type = scopedType, X = 30, Y = 30, W = 200, H = 120 });

        // Sanity: the registry resolves the scoped definition (server-side, no UI).
        registry.GetDef(scopedType, scope).Should().NotBeNull();

        var svg = await RenderToSvgAsync(BuildPageFragment(page, registry, scope));

        svg.Should().Contain("spike-custom-marker");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal interaction-free page → SVG render tree, mirroring the canvas element loop
    /// (background + each element as a translated inner-svg viewport hosting def.RenderSvg).
    /// No selection handles, waypoints, hit-test paths or pointer-events — i.e. the shape
    /// T1's WireframePageSvg.BuildFragment will take.
    /// </summary>
    private static RenderFragment BuildPageFragment(
        WireframePage page, WireframeComponentRegistry registry, WireframeComponentScope? scope)
        => builder =>
        {
            builder.OpenElement(0, "svg");
            builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
            builder.AddAttribute(2, "viewBox", $"0 0 {Fmt(page.Width)} {Fmt(page.Height)}");
            builder.AddAttribute(3, "width", Fmt(page.Width));
            builder.AddAttribute(4, "height", Fmt(page.Height));

            builder.OpenElement(5, "rect");
            builder.AddAttribute(6, "width", Fmt(page.Width));
            builder.AddAttribute(7, "height", Fmt(page.Height));
            builder.AddAttribute(8, "fill", "white");
            builder.CloseElement();

            foreach (var el in page.Elements.OrderBy(e => e.ZIndex))
            {
                var def = registry.GetDef(el.Type, scope);

                builder.OpenElement(9, "g");
                builder.AddAttribute(10, "data-el-id", el.Id);
                builder.AddAttribute(11, "data-type", el.Type);
                builder.AddAttribute(12, "transform", $"translate({Fmt(el.X)}, {Fmt(el.Y)})");

                builder.OpenElement(13, "svg");
                builder.AddAttribute(14, "width", Fmt(el.W));
                builder.AddAttribute(15, "height", Fmt(el.H));
                builder.AddAttribute(16, "viewBox", $"0 0 {Fmt(el.W)} {Fmt(el.H)}");
                builder.AddAttribute(17, "overflow", "visible");

                if (def is not null)
                    builder.AddContent(18, (RenderFragment)(b => def.RenderSvg(el, b)));

                builder.CloseElement();   // inner <svg>
                builder.CloseElement();   // <g>
            }

            builder.CloseElement();       // root <svg>
        };

    /// <summary>Renders a fragment to a markup string via the headless static <see cref="HtmlRenderer"/>.</summary>
    private static async Task<string> RenderToSvgAsync(RenderFragment fragment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        await using var htmlRenderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Content"] = fragment });
            var output = await htmlRenderer.RenderComponentAsync<FragmentHost>(parameters);
            return output.ToHtmlString();
        });
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Tiny component whose only job is to render the supplied fragment.</summary>
    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => Content?.Invoke(builder);
    }
}
