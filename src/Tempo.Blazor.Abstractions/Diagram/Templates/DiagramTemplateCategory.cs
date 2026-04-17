namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>A category grouping of diagram templates displayed in the template gallery.</summary>
public sealed class DiagramTemplateCategory
{
    /// <summary>Display name of the category.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Templates belonging to this category.</summary>
    public List<DiagramTemplate> Templates { get; set; } = [];
}
