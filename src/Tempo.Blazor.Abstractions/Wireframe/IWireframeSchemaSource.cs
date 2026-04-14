using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Provides a set of <see cref="WireframeComponentSchema"/> entries to the
/// <see cref="WireframeSchemaRegistry"/>. Implement this interface to supply
/// component schemas from any source (database, configuration, external API)
/// and register it via <c>services.AddWireframeSchemaSource&lt;T&gt;()</c>.
/// </summary>
public interface IWireframeSchemaSource
{
    /// <summary>Unique identifier for this source (used in logging).</summary>
    string SourceId { get; }

    /// <summary>
    /// Higher priority wins when two sources register the same component type.
    /// Built-in source uses 0; set higher to override.
    /// </summary>
    int Priority { get; }

    /// <summary>Returns all component schemas supplied by this source.</summary>
    IEnumerable<WireframeComponentSchema> GetSchemas();
}
