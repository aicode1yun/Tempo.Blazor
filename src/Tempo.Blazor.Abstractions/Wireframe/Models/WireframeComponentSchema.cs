namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>
/// Pure-data description of a wireframe component's metadata.
/// Contains everything an external consumer (API, MCP server, AI tooling) needs
/// to understand what the component is and how to configure it — without any
/// UI or rendering dependencies.
/// </summary>
/// <remarks>
/// Built-in schemas are provided by <c>BuiltInComponentSchemas</c> in this assembly.
/// Custom schemas (e.g. from a database) can be supplied by implementing
/// <see cref="IWireframeSchemaSource"/> and registering it in DI.
/// </remarks>
public sealed class WireframeComponentSchema
{
    /// <summary>Unique type identifier. Matches <c>WireframeElement.Type</c> in document JSON.</summary>
    public required string Type { get; init; }

    /// <summary>Toolbox / grouping category. E.g. "Buttons", "Inputs", "Layout".</summary>
    public required string Category { get; init; }

    /// <summary>Human-readable component name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Default element width when dropped onto the canvas.</summary>
    public double DefaultWidth { get; init; } = 120;

    /// <summary>Default element height when dropped onto the canvas.</summary>
    public double DefaultHeight { get; init; } = 36;

    /// <summary>
    /// Property definitions that drive the Properties Panel UI and document serialisation.
    /// Each entry describes one key in <c>WireframeElement.Props</c>.
    /// </summary>
    public IReadOnlyList<PropDef> Props { get; init; } = [];

    /// <summary>
    /// Optional map from a <c>size</c> prop value to canonical element dimensions.
    /// Null for components where the size prop does not affect element footprint.
    /// </summary>
    public IReadOnlyDictionary<string, (double W, double H)>? SizePresets { get; init; }
}
