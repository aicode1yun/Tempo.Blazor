namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>
/// Describes a single property of a wireframe component.
/// Drives rendering in the Properties Panel.
/// </summary>
public sealed class PropDef
{
    /// <summary>camelCase property key – matches the key in <see cref="WireframeElement.Props"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable label shown in the Properties Panel.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Data type that determines which input control is rendered.</summary>
    public PropType Type { get; init; } = PropType.String;

    /// <summary>Default value applied when a new element is created.</summary>
    public object? Default { get; init; }

    /// <summary>Allowed values for <see cref="PropType.Enum"/> properties.</summary>
    public string[]? Options { get; init; }

    /// <summary>
    /// Groups related props under a collapsible section header in the Properties Panel.
    /// E.g. "Content", "Appearance", "Behavior".
    /// </summary>
    public string? Category { get; init; }

    /// <summary>When true the Properties Panel marks this field with an asterisk.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Optional regex validated in the Properties Panel (String type only).</summary>
    public string? ValidationRegex { get; init; }
}
