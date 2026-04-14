using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Merges <see cref="WireframeComponentSchema"/> entries from all registered
/// <see cref="IWireframeSchemaSource"/> implementations. When two sources
/// register the same <c>Type</c>, the one with the higher
/// <see cref="IWireframeSchemaSource.Priority"/> wins.
/// </summary>
/// <remarks>
/// Register in DI via <c>services.AddWireframeSchemas()</c> (or
/// <c>AddTempoBlazorAbstractions()</c> which calls it automatically).
/// Typically used as a singleton.
/// </remarks>
public sealed class WireframeSchemaRegistry
{
    private readonly IReadOnlyDictionary<string, WireframeComponentSchema> _index;

    /// <summary>
    /// Builds the registry from the supplied sources, resolving priority conflicts.
    /// </summary>
    public WireframeSchemaRegistry(IEnumerable<IWireframeSchemaSource> sources)
    {
        var map = new Dictionary<string, (WireframeComponentSchema Schema, int Priority)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            foreach (var schema in source.GetSchemas())
            {
                if (!map.TryGetValue(schema.Type, out var existing)
                    || source.Priority >= existing.Priority)
                {
                    map[schema.Type] = (schema, source.Priority);
                }
            }
        }

        _index = map.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Schema,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns all registered schemas ordered by Category then DisplayName.</summary>
    public IEnumerable<WireframeComponentSchema> GetAll()
        => _index.Values
                 .OrderBy(s => s.Category)
                 .ThenBy(s => s.DisplayName);

    /// <summary>Returns the schema for the given component type, or null if not registered.</summary>
    public WireframeComponentSchema? GetSchema(string type)
        => _index.TryGetValue(type, out var s) ? s : null;

    /// <summary>Returns all schemas in a given category.</summary>
    public IEnumerable<WireframeComponentSchema> GetByCategory(string category)
        => _index.Values.Where(s =>
            string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns all distinct category names in registration order.</summary>
    public IEnumerable<string> GetCategories()
        => _index.Values
                 .Select(s => s.Category)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .Order();
}
