using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackCompilerTests
{
    [Fact]
    public void Compile_TwoComponentPack_YieldsTwoNamespacedDefs()
    {
        var pack = Pack("app:demo", "app:demo", isBuiltIn: false, Component("Card"), Component("Badge"));

        var defs = new StencilPackCompiler().Compile(pack).ToList();

        defs.Should().HaveCount(2);
        defs.Select(x => x.Type).Should().BeEquivalentTo(["app:demo:Card", "app:demo:Badge"]);
        defs[0].LocalType.Should().Be("Card");
        defs[0].ScopeAppId.Should().Be("demo");
        defs[0].PackId.Should().Be("app:demo");
        defs[0].IsBuiltIn.Should().BeFalse();
        defs[0].DefaultWidth.Should().Be(160);
        defs[0].DefaultHeight.Should().Be(80);
        defs[0].SizePresets.Should().ContainKey("compact");
        defs[0].SizePresets!["compact"].Should().Be((120, 60));
        defs[0].Impl.Should().NotBeNull();
        defs[0].Impl!.Component.Should().Be("TmCard");
    }

    [Fact]
    public void Compile_BuiltInPack_UsesBareComponentType()
    {
        var pack = Pack("tempo", "tempo", isBuiltIn: true, Component("TmButton"));

        var def = new StencilPackCompiler().Compile(pack).Single();

        def.Type.Should().Be("TmButton");
        def.LocalType.Should().Be("TmButton");
        def.ScopeAppId.Should().BeNull();
        def.IsBuiltIn.Should().BeTrue();
    }

    [Fact]
    public async Task Compile_RenderSvg_DrawsRect()
    {
        var pack = Pack("app:demo", "app:demo", isBuiltIn: false, Component("Card"));
        var registry = new WireframeComponentRegistry();
        foreach (var def in new StencilPackCompiler().Compile(pack))
            registry.RegisterDefinition(def);
        var renderer = BuildRenderer(registry);
        var page = new WireframePage { Name = "Stencil", Width = 320, Height = 180 };
        page.Elements.Add(new WireframeElement { Type = "app:demo:Card", X = 20, Y = 20, W = 160, H = 80 });

        var svg = await renderer.RenderPageAsync(page, WireframeComponentScope.ForApp("demo"));

        svg.Should().Contain("<rect");
        svg.Should().NotContainEquivalentOf("<script");
    }

    [Fact]
    public async Task NativeComponent_ResolvesToCSharpRenderer()
    {
        var nativeRenderers = new NativeRendererRegistry();
        nativeRenderers.Register(
            "TestNative",
            (_, builder) => builder.AddMarkupContent(0, "<g data-native='yes'></g>"));
        var pack = new StencilPack
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = "tempo",
            Namespace = "tempo",
            IsBuiltIn = true,
            Components =
            [
                new StencilComponent
                {
                    Type = "TmNative",
                    DisplayName = "Native",
                    Category = "Native",
                    DefaultSize = new StencilSize(100, 40),
                    Native = new StencilNative { NativeType = "TestNative" }
                }
            ]
        };

        var def = new StencilPackCompiler(nativeRenderers).Compile(pack).Single();
        var svg = await RenderDefAsync(def, new WireframeElement { Type = "TmNative", W = 100, H = 40 });

        def.NativeType.Should().Be("TestNative");
        svg.Should().Contain("data-native='yes'");
    }

    [Fact]
    public void NativeComponent_InUploadedPack_ThrowsInvalidOperationException()
    {
        var pack = new StencilPack
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = "app:demo",
            Namespace = "app:demo",
            Components =
            [
                new StencilComponent
                {
                    Type = "UnsafeNative",
                    DisplayName = "Unsafe",
                    Category = "Native",
                    DefaultSize = new StencilSize(100, 40),
                    Native = new StencilNative { NativeType = "TestNative" }
                }
            ]
        };

        var act = () => new StencilPackCompiler(new NativeRendererRegistry()).Compile(pack).ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NativeComponent_MissingRegisteredRenderer_ThrowsInvalidOperationException()
    {
        var pack = new StencilPack
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = "tempo",
            Namespace = "tempo",
            IsBuiltIn = true,
            Components =
            [
                new StencilComponent
                {
                    Type = "TmNative",
                    DisplayName = "Native",
                    Category = "Native",
                    DefaultSize = new StencilSize(100, 40),
                    Native = new StencilNative { NativeType = "MissingNative" }
                }
            ]
        };

        var act = () => new StencilPackCompiler(new NativeRendererRegistry()).Compile(pack).ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    private static StencilPack Pack(
        string id,
        string ns,
        bool isBuiltIn,
        params StencilComponent[] components)
        => new()
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = id,
            Namespace = ns,
            IsBuiltIn = isBuiltIn,
            Tokens = new Dictionary<string, string>
            {
                ["surface.card"] = "#f8fafc"
            },
            Icons = new Dictionary<string, string>
            {
                ["dot"] = "M1 1 L2 2"
            },
            Components = components
        };

    private static StencilComponent Component(string type)
        => new()
        {
            Type = type,
            DisplayName = type,
            Category = "Tests",
            DefaultSize = new StencilSize(160, 80),
            SizePresets = new Dictionary<string, StencilSize>
            {
                ["compact"] = new(120, 60)
            },
            Props =
            [
                new PropDef
                {
                    Name = "label",
                    DisplayName = "Label",
                    Type = PropType.String,
                    Default = "Card"
                }
            ],
            Impl = new StencilImpl
            {
                Component = "TmCard",
                Parameters = new Dictionary<string, object?>
                {
                    ["ChildContent"] = "{label}"
                }
            },
            Render = new RenderNode
            {
                Kind = RenderNodeKind.Rect,
                Attributes = new Dictionary<string, object?>
                {
                    ["w"] = "{size.w}",
                    ["h"] = "{size.h}",
                    ["fill"] = "token(\"surface.card\")"
                }
            }
        };

    private static WireframeSvgRenderer BuildRenderer(WireframeComponentRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new WireframeSvgRenderer(registry, services.BuildServiceProvider());
    }

    private static async Task<string> RenderDefAsync(WireframeComponentDef def, WireframeElement element)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var htmlRenderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
                builder.AddAttribute(2, "viewBox", $"0 0 {element.W} {element.H}");
                def.RenderSvg(element, builder);
                builder.CloseElement();
            };

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { ["Content"] = fragment });
            var output = await htmlRenderer.RenderComponentAsync<FragmentHost>(parameters);
            return output.ToHtmlString();
        });
    }

    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => Content?.Invoke(builder);
    }
}
