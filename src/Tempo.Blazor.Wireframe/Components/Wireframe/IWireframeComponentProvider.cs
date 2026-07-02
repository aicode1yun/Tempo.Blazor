using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Provides a set of wireframe component definitions to the <see cref="WireframeComponentRegistry"/>.
///
/// <para>
/// Built-in Tempo.Blazor components are registered via <see cref="BuiltInStencilPackProvider"/>.
/// Custom components can be registered by implementing this interface and calling
/// <c>services.AddWireframeComponentProvider&lt;T&gt;()</c> in Program.cs.
/// </para>
///
/// <para>
/// When two providers register the same <see cref="WireframeComponentDef.Type"/>,
/// the one with the higher <see cref="Priority"/> wins.
/// </para>
/// </summary>
public interface IWireframeComponentProvider
{
    /// <summary>Unique identifier for this provider (used in logging and override detection).</summary>
    string ProviderId { get; }

    /// <summary>
    /// Higher priority wins when multiple providers register the same component type.
    /// The built-in Tempo stencil pack uses priority 0; the legacy built-in fallback uses -1.
    /// Custom providers should use values > 0 to override shipped components.
    /// </summary>
    int Priority { get; }

    /// <summary>Returns all component definitions supplied by this provider.</summary>
    IEnumerable<WireframeComponentDef> GetDefinitions();
}

/// <summary>
/// Marker interface for providers whose definitions belong to a single application scope.
/// Registry entries are exposed under <c>app:{ScopeAppId}:{Type}</c>.
/// </summary>
public interface IWireframeScopedComponentProvider : IWireframeComponentProvider
{
    /// <summary>Application id used to namespace the provider's custom component types.</summary>
    string ScopeAppId { get; }
}
