using Tempo.Blazor.Components.Diagram.Templates;

namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>
/// Central registry of all diagram templates.
/// Registered as a singleton DI service.
/// </summary>
public sealed class DiagramTemplateRegistry
{
    private readonly Dictionary<string, (DiagramTemplate Template, int Priority)> _templates = new(StringComparer.Ordinal);

    /// <summary>Loads all template categories from <paramref name="provider"/> into the registry.</summary>
    public void RegisterProvider(IDiagramTemplateProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        foreach (var category in provider.GetTemplateCategories())
        {
            foreach (var template in category.Templates)
                RegisterTemplate(template, provider.Priority);
        }
    }

    /// <summary>
    /// Registers a single template.
    /// If a template with the same id already exists, the one with higher
    /// <paramref name="priority"/> wins (ties keep the existing entry).
    /// </summary>
    public void RegisterTemplate(DiagramTemplate template, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (_templates.TryGetValue(template.Id, out var existing) && existing.Priority >= priority)
            return;
        _templates[template.Id] = (template, priority);
    }

    /// <summary>Returns the template for <paramref name="templateId"/>, or <c>null</c> if not found.</summary>
    public DiagramTemplate? GetTemplate(string templateId)
    {
        _templates.TryGetValue(templateId, out var entry);
        return entry.Template;
    }

    /// <summary>Returns all registered templates ordered by Category then Name.</summary>
    public IEnumerable<DiagramTemplate> GetAll()
        => _templates.Values
                    .Select(e => e.Template)
                    .OrderBy(t => t.Category)
                    .ThenBy(t => t.Name);

    /// <summary>Returns all distinct category names in display order.</summary>
    public string[] GetCategories()
        => _templates.Values
                    .Select(e => e.Template.Category)
                    .Distinct()
                    .Order()
                    .ToArray();

    /// <summary>Returns templates belonging to <paramref name="category"/>.</summary>
    public IEnumerable<DiagramTemplate> GetByCategory(string category)
        => _templates.Values
                    .Where(e => string.Equals(e.Template.Category, category, StringComparison.Ordinal))
                    .Select(e => e.Template)
                    .OrderBy(t => t.Name);

    /// <summary>Total number of registered templates.</summary>
    public int Count => _templates.Count;
}
