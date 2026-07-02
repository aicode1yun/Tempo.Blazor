using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Registry and pack metadata used while resolving stencil composition nodes.</summary>
internal sealed class StencilCompositionScope
{
    public StencilCompositionScope(
        WireframeComponentRegistry? registry,
        WireframeComponentScope? scope,
        StencilPack? currentPack,
        IReadOnlyDictionary<string, StencilPack>? packsByNamespace = null)
    {
        Registry = registry;
        ComponentScope = scope;
        CurrentPack = currentPack;
        PacksByNamespace = packsByNamespace is null || packsByNamespace.Count == 0
            ? new Dictionary<string, StencilPack>(StringComparer.Ordinal)
            : new Dictionary<string, StencilPack>(packsByNamespace, StringComparer.Ordinal);
    }

    public WireframeComponentRegistry? Registry { get; }

    public WireframeComponentScope? ComponentScope { get; }

    public StencilPack? CurrentPack { get; }

    public IReadOnlyDictionary<string, StencilPack> PacksByNamespace { get; }

    public StencilCompositionScope WithCurrentPack(StencilPack? pack)
        => new(Registry, ComponentScope, pack, PacksByNamespace);

    public StencilPack? ResolvePackForType(string type)
    {
        var ns = GetNamespace(type);
        if (string.IsNullOrWhiteSpace(ns))
            return CurrentPack;

        if (CurrentPack is not null && string.Equals(CurrentPack.Namespace, ns, StringComparison.Ordinal))
            return CurrentPack;

        return PacksByNamespace.TryGetValue(ns, out var pack) ? pack : null;
    }

    public static string? GetNamespace(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        var trimmed = type.Trim();
        if (trimmed.StartsWith("app:", StringComparison.OrdinalIgnoreCase))
        {
            var secondSeparator = trimmed.IndexOf(':', "app:".Length);
            return secondSeparator > "app:".Length
                ? trimmed[..secondSeparator]
                : null;
        }

        var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? trimmed[..separator] : null;
    }
}
