namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>Represents a single diagram template that can be instantiated into a new document.</summary>
public sealed class DiagramTemplate
{
    /// <summary>Unique template identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name shown in the gallery.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category name used for grouping in the gallery.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Optional search tags.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Thumbnail image URL (relative or absolute).</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Embedded diagram JSON representing the template content.</summary>
    public string DocumentJson { get; set; } = string.Empty;
}
