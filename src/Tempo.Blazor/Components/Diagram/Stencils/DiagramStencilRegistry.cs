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
        ValidateStencilOrigin(stencil);
        NormalizeStencil(stencil);

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
        => OrderStencils(_stencils.Values.Select(e => e.Stencil));

    /// <summary>Returns all registered Tempo-original stencils ordered by set, palette and display order.</summary>
    public IEnumerable<DiagramStencil> GetTempoOriginal()
        => OrderStencils(_stencils.Values
                    .Select(e => e.Stencil)
                    .Where(s => s.Origin == DiagramStencilOrigin.TempoOriginal));

    /// <summary>Returns all distinct category names in display order.</summary>
    public string[] GetCategories()
        => _stencils.Values
                    .Select(e => e.Stencil.Category)
                    .Distinct()
                    .Order()
                    .ToArray();

    /// <summary>Returns stencils belonging to <paramref name="category"/>.</summary>
    public IEnumerable<DiagramStencil> GetByCategory(string category)
        => OrderStencils(_stencils.Values
                    .Where(e => string.Equals(e.Stencil.Category, category, StringComparison.Ordinal))
                    .Select(e => e.Stencil));

    /// <summary>Searches stencils by name, tags and keywords.</summary>
    public IEnumerable<DiagramStencil> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAll();

        var normalizedQuery = query.Trim();
        return OrderStencils(_stencils.Values
            .Select(e => e.Stencil)
            .Where(stencil => MatchesSearch(stencil, normalizedQuery)));
    }

    /// <summary>Total number of registered stencils.</summary>
    public int Count => _stencils.Count;

    private static void ValidateStencilOrigin(DiagramStencil stencil)
    {
        if (stencil.Origin == DiagramStencilOrigin.Unspecified)
            throw new InvalidOperationException("Diagram stencil origin must be explicitly declared.");
    }

    private static void NormalizeStencil(DiagramStencil stencil)
    {
        stencil.Tags ??= [];
        stencil.Keywords ??= [];
        stencil.Ports ??= [];
        stencil.ConnectionPoints ??= [];
        stencil.DefaultData ??= [];
        stencil.Layout ??= new();
    }

    private static IOrderedEnumerable<DiagramStencil> OrderStencils(IEnumerable<DiagramStencil> stencils)
        => stencils
            .OrderBy(s => SortKey(s.SetId, s.Category), StringComparer.Ordinal)
            .ThenBy(s => SortKey(s.PaletteId, s.Category), StringComparer.Ordinal)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Category, StringComparer.Ordinal)
            .ThenBy(s => s.Name, StringComparer.Ordinal);

    private static bool MatchesSearch(DiagramStencil stencil, string query)
    {
        var fields = GetSearchFields(stencil).ToArray();
        if (fields.Any(field => Contains(field, query)))
            return true;

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 1
            && tokens.All(token => fields.Any(field => Contains(field, token)));
    }

    private static IEnumerable<string?> GetSearchFields(DiagramStencil stencil)
    {
        yield return stencil.Name;
        yield return stencil.Category;
        yield return stencil.SetId;
        yield return stencil.PaletteId;
        foreach (var tag in stencil.Tags)
            yield return tag;
        foreach (var keyword in stencil.Keywords)
            yield return keyword;
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string SortKey(string? metadataValue, string fallbackValue)
        => string.IsNullOrWhiteSpace(metadataValue) ? fallbackValue : metadataValue;
}
