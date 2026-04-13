using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Central registry of all wireframe component definitions.
/// Registered as a singleton DI service.
///
/// Multiple <see cref="IWireframeComponentProvider"/> instances can be registered;
/// when two providers supply the same <see cref="WireframeComponentDef.Type"/>,
/// the definition from the higher-priority provider wins.
/// </summary>
public sealed class WireframeComponentRegistry
{
    // type → (def, providerPriority)
    private readonly Dictionary<string, (WireframeComponentDef Def, int Priority)> _defs = new(StringComparer.Ordinal);

    // ── Provider registration ─────────────────────────────────────────────────

    /// <summary>Loads all definitions from <paramref name="provider"/> into the registry.</summary>
    public void RegisterProvider(IWireframeComponentProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        foreach (var def in provider.GetDefinitions())
            RegisterDefinition(def, provider.Priority);
    }

    /// <summary>
    /// Registers a single definition.
    /// If a definition with the same type already exists, the one with higher
    /// <paramref name="priority"/> wins (ties keep the existing entry).
    /// </summary>
    public void RegisterDefinition(WireframeComponentDef def, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (_defs.TryGetValue(def.Type, out var existing) && existing.Priority >= priority)
            return; // existing has equal or higher priority – keep it
        _defs[def.Type] = (def, priority);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns the definition for <paramref name="type"/>, or <c>null</c> if not found.</summary>
    public WireframeComponentDef? GetDef(string type)
    {
        _defs.TryGetValue(type, out var entry);
        return entry.Def;
    }

    /// <summary>Returns all registered definitions ordered by Category then DisplayName.</summary>
    public IEnumerable<WireframeComponentDef> GetAll()
        => _defs.Values
                .Select(e => e.Def)
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayName);

    /// <summary>Returns all distinct category names in display order.</summary>
    public string[] GetCategories()
        => _defs.Values
                .Select(e => e.Def.Category)
                .Distinct()
                .Order()
                .ToArray();

    /// <summary>Returns definitions belonging to <paramref name="category"/>.</summary>
    public IEnumerable<WireframeComponentDef> GetByCategory(string category)
        => _defs.Values
                .Where(e => string.Equals(e.Def.Category, category, StringComparison.Ordinal))
                .Select(e => e.Def)
                .OrderBy(d => d.DisplayName);

    /// <summary>Total number of registered definitions.</summary>
    public int Count => _defs.Count;
}
