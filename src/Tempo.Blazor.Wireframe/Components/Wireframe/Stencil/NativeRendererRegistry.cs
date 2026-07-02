using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Maps trusted native stencil renderer ids to C# wireframe SVG render actions.</summary>
public sealed class NativeRendererRegistry
{
    private readonly Dictionary<string, Action<WireframeElement, RenderTreeBuilder>> _renderers =
        new(StringComparer.Ordinal);

    /// <summary>Trusted native renderers backed by the built-in Tempo wireframe provider.</summary>
    public static NativeRendererRegistry TempoBuiltIn { get; } = CreateTempoBuiltIn();

    /// <summary>Registers or replaces a native renderer for <paramref name="nativeType"/>.</summary>
    public void Register(string nativeType, Action<WireframeElement, RenderTreeBuilder> renderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeType);
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers[nativeType.Trim()] = renderer;
    }

    /// <summary>Attempts to resolve a native renderer by id.</summary>
    public bool TryGet(
        string? nativeType,
        out Action<WireframeElement, RenderTreeBuilder>? renderer)
    {
        renderer = null;
        return !string.IsNullOrWhiteSpace(nativeType)
               && _renderers.TryGetValue(nativeType.Trim(), out renderer);
    }

    private static NativeRendererRegistry CreateTempoBuiltIn()
    {
        var registry = new NativeRendererRegistry();
        foreach (var (nativeType, renderer) in TempoNativeRendererProvider.GetRenderers())
            registry.Register(nativeType, renderer);
        return registry;
    }
}
