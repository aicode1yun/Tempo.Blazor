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

public class StencilPackRendererCompositionTests
{
    [Fact]
    public async Task Card_EmbeddingTempoBadge_RendersBadgeSvgWithBoundProps()
    {
        var pack = Pack(
            "tempo",
            Target(),
            CardComponent("tempo:Card", "tempo:Badge"),
            BadgeComponent("tempo:Badge", "#dcfce7"));
        var registry = RegistryFor(pack);

        var svg = await RenderAsync(
            pack.Components.Single(x => x.Type == "tempo:Card"),
            Element("tempo:Card", 260, 140, ("status", "Paid")),
            pack,
            registry);

        svg.Should().Contain(">Paid<");
        svg.Should().Contain("fill='#dcfce7'");
        svg.Should().Contain("width='82' height='24'");
        svg.Should().NotContainEquivalentOf("<script");
    }

    [Fact]
    public async Task SiblingReferencesToSameComponent_RenderIndependentlyWithoutCyclePlaceholder()
    {
        var card = Component(
            "tempo:Card",
            new RenderNode
            {
                Kind = RenderNodeKind.Group,
                Children =
                [
                    Node(RenderNodeKind.Rect, ("w", "{size.w}"), ("h", "{size.h}"), ("fill", "#ffffff")),
                    new RenderNode
                    {
                        Kind = RenderNodeKind.Component,
                        Attributes = Attrs(("ref", "tempo:Badge"), ("x", 10), ("y", 12)),
                        Props = new Dictionary<string, object?>
                        {
                            ["label"] = "Approved"
                        }
                    },
                    new RenderNode
                    {
                        Kind = RenderNodeKind.Component,
                        Attributes = Attrs(("ref", "tempo:Badge"), ("x", 110), ("y", 12)),
                        Props = new Dictionary<string, object?>
                        {
                            ["label"] = "Pending"
                        }
                    }
                ]
            },
            220,
            56);
        var badge = BadgeComponent("tempo:Badge", "#dcfce7");
        var pack = Pack("tempo", Target(), card, badge);
        var registry = RegistryFor(pack);

        var svg = await RenderAsync(card, Element("tempo:Card", 220, 56), pack, registry);

        svg.Should().Contain(">Approved<");
        svg.Should().Contain(">Pending<");
        svg.Should().NotContain("stroke-dasharray");
    }

    [Fact]
    public async Task DiamondReferencesToSameLeaf_RenderLeafForEachBranchWithoutCyclePlaceholder()
    {
        var leaf = Component(
            "graph:D",
            Node(RenderNodeKind.Text, ("content", "Leaf"), ("x", 0), ("y", 12)),
            60,
            20);
        var branchB = Component(
            "graph:B",
            Node(RenderNodeKind.Component, ("ref", "graph:D")),
            60,
            20);
        var branchC = Component(
            "graph:C",
            Node(RenderNodeKind.Component, ("ref", "graph:D")),
            60,
            20);
        var root = Component(
            "graph:A",
            new RenderNode
            {
                Kind = RenderNodeKind.Group,
                Children =
                [
                    Node(RenderNodeKind.Component, ("ref", "graph:B"), ("x", 0), ("y", 0)),
                    Node(RenderNodeKind.Component, ("ref", "graph:C"), ("x", 80), ("y", 0))
                ]
            },
            160,
            40);
        var pack = Pack("graph", Target(), root, branchB, branchC, leaf);
        var registry = RegistryFor(pack);

        var svg = await RenderAsync(root, Element("graph:A", 160, 40), pack, registry);

        CountOccurrences(svg, ">Leaf<").Should().Be(2);
        svg.Should().NotContain("stroke-dasharray");
    }

    [Fact]
    public async Task Parts_AreInlined_ShareParentProps()
    {
        var part = Node(RenderNodeKind.Text, ("content", "{status}"), ("x", 16), ("y", 28), ("fontSize", 12));
        var card = Component(
            "local:Card",
            new RenderNode
            {
                Kind = RenderNodeKind.Group,
                Children =
                [
                    Node(RenderNodeKind.Rect, ("w", "{size.w}"), ("h", "{size.h}")),
                    Node(RenderNodeKind.Part, ("name", "statusLine"))
                ]
            },
            180,
            80);
        var pack = Pack("local", Target(), [card], new Dictionary<string, RenderNode>
        {
            ["statusLine"] = part
        });
        var registry = RegistryFor(pack);

        var svg = await RenderAsync(card, Element("local:Card", 180, 80, ("status", "Draft")), pack, registry);

        svg.Should().Contain(">Draft<");
        svg.Should().NotContain("stroke-dasharray");
    }

    [Fact]
    public async Task SelfReference_IsGuarded_NoStackOverflow()
    {
        var component = Component(
            "loop:Self",
            Node(RenderNodeKind.Component, ("ref", "loop:Self")),
            120,
            36);
        var pack = Pack("loop", Target(), component);
        var registry = RegistryFor(pack);

        var act = () => RenderAsync(component, Element("loop:Self", 120, 36), pack, registry);

        var assertion = await act.Should().NotThrowAsync();
        assertion.Subject.Should().Contain("stroke-dasharray");
    }

    [Fact]
    public async Task DepthCap_IsRespected()
    {
        var components = Enumerable.Range(0, 18)
            .Select(i => Component(
                $"chain:C{i}",
                i == 17
                    ? Node(RenderNodeKind.Text, ("content", "Terminal"), ("x", 0), ("y", 12))
                    : Node(RenderNodeKind.Component, ("ref", $"chain:C{i + 1}")),
                120,
                36))
            .ToArray();
        var pack = Pack("chain", Target(), components);
        var registry = RegistryFor(pack);

        var svg = await RenderAsync(components[0], Element("chain:C0", 120, 36), pack, registry);

        svg.Should().Contain("stroke-dasharray");
        svg.Should().NotContain("Terminal");
    }

    [Fact]
    public async Task CrossNamespace_DifferentTarget_RendersPlaceholder()
    {
        var host = Pack(
            "host",
            Target(framework: "Blazor", library: "Tempo.Blazor"),
            Component("host:Card", Node(RenderNodeKind.Component, ("ref", "foreign:Badge")), 180, 80));
        var foreign = Pack(
            "foreign",
            Target(framework: "React", library: "Other.Ui"),
            BadgeComponent("foreign:Badge", "#fee2e2", "Foreign Badge"));
        var registry = RegistryFor(host, foreign);

        var svg = await RenderAsync(
            host.Components.Single(),
            Element("host:Card", 180, 80),
            host,
            registry,
            host,
            foreign);

        svg.Should().Contain("stroke-dasharray");
        svg.Should().NotContain("Foreign Badge");
    }

    [Fact]
    public async Task CrossNamespace_SameTarget_Embeds()
    {
        var host = Pack(
            "host",
            Target(framework: "Blazor", library: "Tempo.Blazor", version: "1.0.0"),
            Component("host:Card", Node(RenderNodeKind.Component, ("ref", "shared:Badge")), 180, 80));
        var shared = Pack(
            "shared",
            Target(framework: "Blazor", library: "Tempo.Blazor", version: "2.0.0"),
            BadgeComponent("shared:Badge", "#dbeafe", "Shared Badge"));
        var registry = RegistryFor(host, shared);

        var svg = await RenderAsync(
            host.Components.Single(),
            Element("host:Card", 180, 80),
            host,
            registry,
            host,
            shared);

        svg.Should().Contain(">Shared Badge<");
        svg.Should().NotContain("stroke-dasharray");
    }

    private static StencilComponent CardComponent(string type, string badgeRef)
        => Component(
            type,
            new RenderNode
            {
                Kind = RenderNodeKind.Group,
                Children =
                [
                    Node(RenderNodeKind.Rect, ("w", "{size.w}"), ("h", "{size.h}"), ("fill", "#ffffff"), ("stroke", "#cbd5e1"), ("rx", 8)),
                    Node(RenderNodeKind.Text, ("content", "Invoice"), ("x", 18), ("y", 28), ("fontSize", 14), ("fontWeight", "600")),
                    new RenderNode
                    {
                        Kind = RenderNodeKind.Component,
                        Attributes = Attrs(("ref", badgeRef), ("x", 158), ("y", 18)),
                        Props = new Dictionary<string, object?>
                        {
                            ["label"] = "{status}"
                        }
                    }
                ]
            },
            260,
            140);

    private static StencilComponent BadgeComponent(string type, string fill, string label = "{label}")
        => Component(
            type,
            new RenderNode
            {
                Kind = RenderNodeKind.Group,
                Children =
                [
                    Node(RenderNodeKind.Rect, ("w", "{size.w}"), ("h", "{size.h}"), ("fill", fill), ("stroke", "#86efac"), ("rx", 12)),
                    Node(RenderNodeKind.Text, ("content", label), ("x", 0), ("y", 0), ("w", "{size.w}"), ("h", "{size.h}"), ("align", "center"), ("fontSize", 11), ("fontWeight", "600"))
                ]
            },
            82,
            24);

    private static StencilComponent Component(string type, RenderNode render, double width, double height)
        => new()
        {
            Type = type,
            DisplayName = type,
            Category = "Tests",
            DefaultSize = new StencilSize(width, height),
            Render = render
        };

    private static StencilPack Pack(string ns, StencilTarget? target, params StencilComponent[] components)
        => Pack(ns, target, components, new Dictionary<string, RenderNode>());

    private static StencilPack Pack(
        string ns,
        StencilTarget? target,
        IReadOnlyList<StencilComponent> components,
        IReadOnlyDictionary<string, RenderNode> parts)
        => new()
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = ns + "-pack",
            Namespace = ns,
            Target = target,
            Parts = parts,
            Components = components
        };

    private static StencilTarget Target(
        string framework = "Blazor",
        string library = "Tempo.Blazor",
        string version = "1.0.0")
        => new()
        {
            Framework = framework,
            Library = library,
            Version = version
        };

    private static RenderNode Node(RenderNodeKind kind, params (string Key, object? Value)[] attributes)
        => new()
        {
            Kind = kind,
            Attributes = Attrs(attributes)
        };

    private static Dictionary<string, object?> Attrs(params (string Key, object? Value)[] attributes)
        => attributes.ToDictionary(x => x.Key, x => x.Value);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static WireframeElement Element(string type, double w, double h, params (string Key, object? Value)[] props)
    {
        var element = new WireframeElement { Type = type, W = w, H = h };
        foreach (var (key, value) in props)
            element.Props[key] = JsonSerializer.SerializeToElement(value);
        return element;
    }

    private static WireframeComponentRegistry RegistryFor(params StencilPack[] packs)
    {
        var registry = new WireframeComponentRegistry();
        var packMap = packs.ToDictionary(x => x.Namespace, StringComparer.Ordinal);

        foreach (var pack in packs)
        {
            foreach (var component in pack.Components)
            {
                var capturedPack = pack;
                var capturedComponent = component;
                registry.RegisterDefinition(new WireframeComponentDef
                {
                    Type = component.Type,
                    DisplayName = component.DisplayName,
                    Category = component.Category,
                    DefaultWidth = component.DefaultSize.Width,
                    DefaultHeight = component.DefaultSize.Height,
                    SizePresets = component.SizePresets.ToDictionary(
                        x => x.Key,
                        x => (x.Value.Width, x.Value.Height),
                        StringComparer.Ordinal),
                    IsBuiltIn = component.Type.StartsWith("tempo:", StringComparison.Ordinal),
                    RenderSvg = (element, builder) => StencilPackRenderer.Render(
                        capturedComponent,
                        element,
                        StencilTokenScope.Empty,
                        builder,
                        new StencilCompositionScope(registry, scope: null, capturedPack, packMap),
                        logger: null)
                });
            }
        }

        return registry;
    }

    private static async Task<string> RenderAsync(
        StencilComponent component,
        WireframeElement element,
        StencilPack pack,
        WireframeComponentRegistry registry,
        params StencilPack[] allPacks)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var htmlRenderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        var packMap = (allPacks.Length == 0 ? [pack] : allPacks)
            .ToDictionary(x => x.Namespace, StringComparer.Ordinal);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
                builder.AddAttribute(2, "viewBox", $"0 0 {element.W} {element.H}");
                StencilPackRenderer.Render(
                    component,
                    element,
                    StencilTokenScope.Empty,
                    builder,
                    new StencilCompositionScope(registry, scope: null, pack, packMap),
                    logger: null);
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
