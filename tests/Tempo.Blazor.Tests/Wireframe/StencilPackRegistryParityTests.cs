using FluentAssertions.Execution;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackRegistryParityTests
{
    private static readonly string[] GoldenDeclarativeTypes =
    [
        "TmStepper",
        "TmTimeline",
        "TmTreeView",
        "TmTreeList",
        "TmActivityLog",
        "TmActivityTimeline",
        "TmImageGallery",
        "TmAIPrompt",
        "TmShareLinkPanel",
        "TmSparkline",
        "TmDashboard",
        "TmScheduler"
    ];

    public static TheoryData<string> GoldenDeclarativeTypeData
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in GoldenDeclarativeTypes)
                data.Add(type);
            return data;
        }
    }

    public static TheoryData<string> BuiltInSchemaTypeData
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in new BuiltInComponentSchemas().GetSchemas().Select(schema => schema.Type))
                data.Add(type);
            return data;
        }
    }

    [Fact]
    public void Registry_CoversEveryBuiltInSchema_ThroughPack()
    {
        var registry = Registry();
        var canonicalTypes = new BuiltInComponentSchemas()
            .GetSchemas()
            .Select(schema => schema.Type)
            .ToArray();

        using var _ = new AssertionScope();
        foreach (var type in canonicalTypes)
            registry.GetDef(type).Should().NotBeNull($"{type} must resolve through the Tempo stencil pack");
    }

    [Theory]
    [MemberData(nameof(GoldenDeclarativeTypeData))]
    public async Task DeclarativeGoldenTypes_RenderSafeSvg(string type)
    {
        var registry = Registry();
        var def = registry.GetDef(type) ?? throw new InvalidOperationException($"Missing definition for {type}.");
        var page = PageWith(def);

        var svg = await Renderer(registry).RenderPageAsync(page);

        using var _ = new AssertionScope(type);
        def.PackId.Should().Be("tempo");
        def.NativeType.Should().BeNull();
        svg.Should().StartWith("<svg");
        svg.Should().Contain("<rect");
        svg.Should().Contain("<text");
        svg.Should().NotContainEquivalentOf("<script");
        svg.Should().NotContainEquivalentOf("javascript:");
        svg.Should().NotContainEquivalentOf("onerror=");
        svg.Should().NotContainEquivalentOf("<foreignObject");
    }

    [Theory]
    [MemberData(nameof(BuiltInSchemaTypeData))]
    public async Task EveryBuiltInSchemaType_RendersSafeSvgThroughPack(string type)
    {
        var registry = Registry();
        var def = registry.GetDef(type) ?? throw new InvalidOperationException($"Missing definition for {type}.");
        var element = ElementFor(def);

        var act = () => def.RenderSvg(element, new RenderTreeBuilder());
        var svg = await Renderer(registry).RenderPageAsync(PageWith(element));

        using var _ = new AssertionScope(type);
        act.Should().NotThrow($"{type} should render through the Tempo stencil pack");
        svg.Should().StartWith("<svg");
        svg.Should().NotContainEquivalentOf("<script");
        svg.Should().NotContainEquivalentOf("javascript:");
        svg.Should().NotContainEquivalentOf("onerror=");
        svg.Should().NotContainEquivalentOf("<foreignObject");
    }

    private static WireframePage PageWith(WireframeComponentDef def)
        => PageWith(ElementFor(def));

    private static WireframePage PageWith(WireframeElement element)
    {
        var page = new WireframePage
        {
            Id = "phase12",
            Name = "Phase 12",
            Width = Math.Max(700, element.W + 80),
            Height = Math.Max(420, element.H + 80)
        };
        page.Elements.Add(element);
        return page;
    }

    private static WireframeElement ElementFor(WireframeComponentDef def)
        => new()
        {
            Id = "sut",
            Type = def.Type,
            X = 40,
            Y = 40,
            W = def.DefaultWidth,
            H = def.DefaultHeight
        };

    private static WireframeSvgRenderer Renderer(WireframeComponentRegistry registry)
        => new(registry, Services());

    private static WireframeComponentRegistry Registry()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterProvider(new BuiltInStencilPackProvider());
        return registry;
    }

    private static IServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
