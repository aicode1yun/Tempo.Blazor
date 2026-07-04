using System.Text.Json.Serialization;

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

    /// <summary>
    /// Application id for an app-scoped custom component. When present, registries normalize
    /// <see cref="Type"/> to <c>app:{ScopeAppId}:{LocalType}</c>.
    /// </summary>
    public string? ScopeAppId { get; init; }

    /// <summary>
    /// Component type name without the app scope prefix. Defaults to <see cref="Type"/> for
    /// unscoped components and to the suffix of <c>app:{id}:{name}</c> for scoped components.
    /// </summary>
    public string? LocalType { get; init; }

    /// <summary>Toolbox / grouping category. E.g. "Buttons", "Inputs", "Layout".</summary>
    public required string Category { get; init; }

    /// <summary>Human-readable component name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Library-agnostic UI roles that this component can satisfy, such as
    /// <c>search-input</c> or <c>data-table</c>.
    /// </summary>
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>True for built-in Tempo.Blazor components; false for app/custom schemas.</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// True when the component is intended to visually contain other placed elements.
    /// Used by authoring/lint tools to distinguish parent-like containment from sibling overlap.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsContainer { get; init; }

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
