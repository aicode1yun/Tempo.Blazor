using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Provides a set of wireframe component definitions to the <see cref="WireframeComponentRegistry"/>.
///
/// <para>
/// Built-in Tempo.Blazor components are registered via <see cref="BuiltInWireframeComponentProvider"/>.
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
    /// Built-in provider uses priority 0; custom providers should use values > 0 to override.
    /// </summary>
    int Priority { get; }

    /// <summary>Returns all component definitions supplied by this provider.</summary>
    IEnumerable<WireframeComponentDef> GetDefinitions();
}
