using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Exposes compiled stencil pack components to the wireframe component registry.</summary>
public sealed class StencilPackComponentProvider : IWireframeScopedComponentProvider
{
    private static readonly Lazy<UiRoleVocabulary> BuiltInVocabulary =
        new(() => new UiRoleVocabulary([new BuiltInUiRoleVocabularySource()]));

    private readonly StencilPack _pack;
    private readonly StencilPackCompiler _compiler;
    private readonly UiRoleVocabulary _roleVocabulary;
    private readonly Lazy<StencilPackCompilationResult> _result;

    public StencilPackComponentProvider(
        StencilPack pack,
        string? scopeAppIdOrNamespace = null,
        int priority = 50)
        : this(
            pack,
            new StencilPackCompiler(),
            BuiltInVocabulary.Value,
            priority,
            ResolveScopeAppId(scopeAppIdOrNamespace, pack?.Namespace))
    {
    }

    public StencilPackComponentProvider(
        StencilPack pack,
        UiRoleVocabulary roleVocabulary,
        string? scopeAppIdOrNamespace = null,
        int priority = 50)
        : this(
            pack,
            new StencilPackCompiler(),
            roleVocabulary,
            priority,
            ResolveScopeAppId(scopeAppIdOrNamespace, pack?.Namespace))
    {
    }

    public StencilPackComponentProvider(
        StencilPack pack,
        StencilPackCompiler compiler,
        int priority = 50)
        : this(pack, compiler, BuiltInVocabulary.Value, priority, ResolveScopeAppId(null, pack?.Namespace))
    {
    }

    public StencilPackComponentProvider(
        StencilPack pack,
        StencilPackCompiler compiler,
        UiRoleVocabulary roleVocabulary,
        int priority = 50)
        : this(pack, compiler, roleVocabulary, priority, ResolveScopeAppId(null, pack?.Namespace))
    {
    }

    private StencilPackComponentProvider(
        StencilPack pack,
        StencilPackCompiler compiler,
        UiRoleVocabulary roleVocabulary,
        int priority,
        string scopeAppId)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(roleVocabulary);
        _pack = pack;
        _compiler = compiler;
        _roleVocabulary = roleVocabulary;
        _result = new Lazy<StencilPackCompilationResult>(() =>
            _compiler.CompileWithDiagnostics(_pack, _roleVocabulary));
        Priority = priority;
        ScopeAppId = scopeAppId;
    }

    public string ProviderId => "stencil:" + _pack.Id;

    public int Priority { get; }

    public string ScopeAppId { get; }

    public IReadOnlyList<StencilPackValidationWarning> ValidationWarnings => _result.Value.Warnings;

    public IEnumerable<WireframeComponentDef> GetDefinitions()
        => _result.Value.Definitions;

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
