using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Configuration;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Faze T (T3) tests: <see cref="WireframeServiceCollectionExtensions.AddTempoBlazorWireframe"/>
/// registers <see cref="IWireframeSvgRenderer"/>, and the resolved instance renders using the
/// DI-built component registry (with the built-in providers wired up).
/// </summary>
public class WireframeSvgRendererDiTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTempoBlazorWireframe();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddTempoBlazorWireframe_RegistersSvgRenderer()
    {
        using var provider = BuildProvider();

        var renderer = provider.GetService<IWireframeSvgRenderer>();

        renderer.Should().NotBeNull();
        renderer.Should().BeOfType<WireframeSvgRenderer>();
    }

    [Fact]
    public void SvgRenderer_IsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IWireframeSvgRenderer>();
        var second = provider.GetRequiredService<IWireframeSvgRenderer>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task ResolvedRenderer_RendersBuiltInComponent_FromDiRegistry()
    {
        using var provider = BuildProvider();
        var renderer = provider.GetRequiredService<IWireframeSvgRenderer>();

        var page = new WireframePage { Name = "P", Width = 400, Height = 300 };
        page.Elements.Add(new WireframeElement { Type = "TmButton", X = 20, Y = 20, W = 120, H = 36 });

        var svg = await renderer.RenderPageAsync(page);

        svg.Should().StartWith("<svg");
        svg.Should().Contain("<rect");   // built-in TmButton resolved via the DI-built registry
    }
}
