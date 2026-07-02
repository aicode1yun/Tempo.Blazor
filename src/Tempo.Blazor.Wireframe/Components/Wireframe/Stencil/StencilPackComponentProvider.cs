using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Exposes compiled stencil pack components to the wireframe component registry.</summary>
public sealed class StencilPackComponentProvider : IWireframeScopedComponentProvider
{
    private readonly StencilPack _pack;
    private readonly StencilPackCompiler _compiler;

    public StencilPackComponentProvider(
        StencilPack pack,
        string? scopeAppIdOrNamespace = null,
        int priority = 50)
        : this(pack, new StencilPackCompiler(), priority, ResolveScopeAppId(scopeAppIdOrNamespace, pack?.Namespace))
    {
    }

    public StencilPackComponentProvider(
        StencilPack pack,
        StencilPackCompiler compiler,
        int priority = 50)
        : this(pack, compiler, priority, ResolveScopeAppId(null, pack?.Namespace))
    {
    }

    private StencilPackComponentProvider(
        StencilPack pack,
        StencilPackCompiler compiler,
        int priority,
        string scopeAppId)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(compiler);
        _pack = pack;
        _compiler = compiler;
        Priority = priority;
        ScopeAppId = scopeAppId;
    }

    public string ProviderId => "stencil:" + _pack.Id;

    public int Priority { get; }

    public string ScopeAppId { get; }

    public IEnumerable<WireframeComponentDef> GetDefinitions()
        => _compiler.Compile(_pack);

    private static string ResolveScopeAppId(string? preferred, string? packNamespace)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var explicitAppId = StencilPackCompiler.TryGetAppScopeId(preferred);
            return explicitAppId ?? preferred.Trim();
        }

        return StencilPackCompiler.TryGetAppScopeId(packNamespace) ?? string.Empty;
    }
}
