using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>
/// Central registry of all diagram stencil definitions.
/// Registered as a singleton DI service.
///
/// Multiple <see cref="IDiagramStencilProvider"/> instances can be registered;
/// when two providers supply the same <see cref="DiagramStencil.Id"/>,
/// the definition from the higher-priority provider wins.
/// </summary>
public sealed class DiagramStencilRegistry
{
    // stencilId → (stencil, providerPriority)
    private readonly Dictionary<string, (DiagramStencil Stencil, int Priority)> _stencils = new(StringComparer.Ordinal);

    /// <summary>Loads all stencil sets from <paramref name="provider"/> into the registry.</summary>
    public void RegisterProvider(IDiagramStencilProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        foreach (var set in provider.GetStencilSets())
        {
            foreach (var stencil in set.Stencils)
                RegisterStencil(stencil, provider.Priority);
        }
    }

    /// <summary>
    /// Registers a single stencil.
    /// If a stencil with the same id already exists, the one with higher
    /// <paramref name="priority"/> wins (ties keep the existing entry).
    /// </summary>
    public void RegisterStencil(DiagramStencil stencil, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(stencil);
        if (_stencils.TryGetValue(stencil.Id, out var existing) && existing.Priority >= priority)
            return;
        _stencils[stencil.Id] = (stencil, priority);
    }

    /// <summary>Returns the stencil for <paramref name="stencilId"/>, or <c>null</c> if not found.</summary>
    public DiagramStencil? GetStencil(string stencilId)
    {
        _stencils.TryGetValue(stencilId, out var entry);
        return entry.Stencil;
    }

    /// <summary>Returns all registered stencils ordered by Category then Name.</summary>
    public IEnumerable<DiagramStencil> GetAll()
        => _stencils.Values
                    .Select(e => e.Stencil)
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.Name);

    /// <summary>Returns all distinct category names in display order.</summary>
    public string[] GetCategories()
        => _stencils.Values
                    .Select(e => e.Stencil.Category)
                    .Distinct()
                    .Order()
                    .ToArray();

    /// <summary>Returns stencils belonging to <paramref name="category"/>.</summary>
    public IEnumerable<DiagramStencil> GetByCategory(string category)
        => _stencils.Values
                    .Where(e => string.Equals(e.Stencil.Category, category, StringComparison.Ordinal))
                    .Select(e => e.Stencil)
                    .OrderBy(s => s.Name);

    /// <summary>Total number of registered stencils.</summary>
    public int Count => _stencils.Count;
}
