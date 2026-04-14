namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A layout section inside a diagram stencil.</summary>
public sealed class DiagramStencilSection
{
    /// <summary>Section type. Supported: "text", "list", "divider", "icon".</summary>
    public string Type { get; set; } = "text";

    /// <summary>Data key referencing <see cref="DiagramNode.Data"/> (e.g. "name", "attributes").</summary>
    public string? DataKey { get; set; }

    /// <summary>Fallback text when the data key is missing or empty.</summary>
    public string? DefaultText { get; set; }

    /// <summary>Optional CSS class for fine-grained styling.</summary>
    public string? CssClass { get; set; }

    /// <summary>Uniform padding in pixels for this section.</summary>
    public double? Padding { get; set; }

    /// <summary>Text style overrides for this section.</summary>
    public DiagramStencilTextStyle? TextStyle { get; set; }
}
