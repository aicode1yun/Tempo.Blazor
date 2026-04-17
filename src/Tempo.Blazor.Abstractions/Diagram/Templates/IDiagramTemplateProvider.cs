namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>Provides diagram template categories to the <see cref="DiagramTemplateRegistry"/>.</summary>
public interface IDiagramTemplateProvider
{
    /// <summary>Loading priority. Higher values win when duplicates exist.</summary>
    int Priority { get; }

    /// <summary>Returns all template categories provided by this source.</summary>
    Task<IEnumerable<DiagramTemplateCategory>> GetTemplateCategoriesAsync();
}
