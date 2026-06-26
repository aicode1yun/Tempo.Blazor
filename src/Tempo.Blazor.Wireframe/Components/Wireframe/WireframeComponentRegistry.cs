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
        var scopeAppId = provider is IWireframeScopedComponentProvider scopedProvider
            ? scopedProvider.ScopeAppId
            : null;

        foreach (var def in provider.GetDefinitions())
            RegisterDefinition(def, provider.Priority, scopeAppId);
    }

    /// <summary>
    /// Registers a single definition.
    /// If a definition with the same type already exists, the one with higher
    /// <paramref name="priority"/> wins (ties keep the existing entry).
    /// </summary>
    public void RegisterDefinition(WireframeComponentDef def, int priority = 0)
        => RegisterDefinition(def, priority, scopeAppId: null);

    /// <summary>Registers a single definition in an application component scope.</summary>
    public void RegisterDefinition(WireframeComponentDef def, string scopeAppId)
        => RegisterDefinition(def, priority: 0, scopeAppId);

    /// <summary>Registers a single definition in an application component scope.</summary>
    public void RegisterDefinition(WireframeComponentDef def, int priority, string? scopeAppId)
    {
        ArgumentNullException.ThrowIfNull(def);
        var normalized = NormalizeDefinition(def, scopeAppId);

        if (_defs.TryGetValue(normalized.Type, out var existing) && existing.Priority >= priority)
            return; // existing has equal or higher priority – keep it

        _defs[normalized.Type] = (normalized, priority);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns the definition for <paramref name="type"/>, or <c>null</c> if not found.</summary>
    public WireframeComponentDef? GetDef(string type)
    {
        _defs.TryGetValue(type, out var entry);
        return entry.Def;
    }

    /// <summary>
    /// Returns the definition for <paramref name="type"/> in <paramref name="scope"/>, or
    /// <c>null</c> if not found. Local custom names are resolved as <c>app:{id}:{type}</c>.
    /// </summary>
    public WireframeComponentDef? GetDef(string type, WireframeComponentScope? scope)
    {
        if (scope is null)
            return GetDef(type);

        if (WireframeComponentScope.IsScopedType(type))
            return scope.ContainsType(type) ? GetDef(type) : null;

        var scopedType = scope.NamespaceType(type);
        var scopedDef = GetDef(scopedType);
        if (scopedDef is not null)
            return scopedDef;

        var baselineDef = GetDef(type);
        return baselineDef?.IsBuiltIn == true ? baselineDef : null;
    }

    /// <summary>Returns all registered definitions ordered by Category then DisplayName.</summary>
    public IEnumerable<WireframeComponentDef> GetAll()
        => VisibleDefinitions(scope: null)
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayName);

    /// <summary>
    /// Returns built-in definitions plus custom definitions for <paramref name="scope"/>.
    /// When <paramref name="scope"/> is null, app-scoped custom definitions are hidden.
    /// </summary>
    public IEnumerable<WireframeComponentDef> GetAll(WireframeComponentScope? scope)
        => VisibleDefinitions(scope)
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayName);

    /// <summary>Returns all distinct category names in display order.</summary>
    public string[] GetCategories()
        => GetCategories(scope: null);

    /// <summary>Returns all distinct category names in display order for <paramref name="scope"/>.</summary>
    public string[] GetCategories(WireframeComponentScope? scope)
        => VisibleDefinitions(scope)
                .Select(d => d.Category)
                .Distinct()
                .Order()
                .ToArray();

    /// <summary>Returns definitions belonging to <paramref name="category"/>.</summary>
    public IEnumerable<WireframeComponentDef> GetByCategory(string category)
        => GetByCategory(category, scope: null);

    /// <summary>Returns definitions belonging to <paramref name="category"/> in <paramref name="scope"/>.</summary>
    public IEnumerable<WireframeComponentDef> GetByCategory(string category, WireframeComponentScope? scope)
        => VisibleDefinitions(scope)
                .Where(d => string.Equals(d.Category, category, StringComparison.Ordinal))
                .OrderBy(d => d.DisplayName);

    /// <summary>Total number of registered definitions.</summary>
    public int Count => _defs.Count;

    private IEnumerable<WireframeComponentDef> VisibleDefinitions(WireframeComponentScope? scope)
        => _defs.Values
                .Select(e => e.Def)
                .Where(d => scope is null
                    ? d.ScopeAppId is null
                    : d.IsBuiltIn || (d.ScopeAppId is not null && scope.MatchesAppId(d.ScopeAppId)));

    private static WireframeComponentDef NormalizeDefinition(WireframeComponentDef def, string? scopeAppId)
    {
        var effectiveScopeAppId = ResolveScopeAppId(def.Type, def.ScopeAppId, scopeAppId);
        if (effectiveScopeAppId is null)
            return def;

        var scope = WireframeComponentScope.ForApp(effectiveScopeAppId);
        var localType = string.IsNullOrWhiteSpace(def.LocalType)
            ? WireframeComponentScope.GetLocalType(def.Type)
            : def.LocalType.Trim();
        var normalizedType = scope.NamespaceType(localType);

        if (string.Equals(def.Type, normalizedType, StringComparison.Ordinal)
            && string.Equals(def.ScopeAppId, scope.AppId, StringComparison.Ordinal)
            && string.Equals(def.LocalType, localType, StringComparison.Ordinal))
        {
            return def;
        }

        return new WireframeComponentDef
        {
            Type = normalizedType,
            ScopeAppId = scope.AppId,
            LocalType = localType,
            Category = def.Category,
            DisplayName = def.DisplayName,
            Icon = def.Icon,
            DefaultWidth = def.DefaultWidth,
            DefaultHeight = def.DefaultHeight,
            Props = def.Props,
            RenderSvg = def.RenderSvg,
            IsBuiltIn = def.IsBuiltIn,
            SizePresets = def.SizePresets,
        };
    }

    private static string? ResolveScopeAppId(string type, string? definitionScopeAppId, string? providerScopeAppId)
    {
        if (!string.IsNullOrWhiteSpace(providerScopeAppId))
            return providerScopeAppId;

        if (!string.IsNullOrWhiteSpace(definitionScopeAppId))
            return definitionScopeAppId;

        return WireframeComponentScope.TryGetAppId(type, out var parsedAppId)
            ? parsedAppId
            : null;
    }
}
