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
            var sourceScopeAppId = source is IWireframeScopedSchemaSource scopedSource
                ? scopedSource.ScopeAppId
                : null;
            var isBuiltInSource = string.Equals(source.SourceId, "BuiltIn", StringComparison.Ordinal);

            foreach (var schema in source.GetSchemas())
            {
                var normalized = NormalizeSchema(schema, sourceScopeAppId, isBuiltInSource);
                if (!map.TryGetValue(normalized.Type, out var existing)
                    || source.Priority >= existing.Priority)
                {
                    map[normalized.Type] = (normalized, source.Priority);
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
                 .Where(s => s.ScopeAppId is null)
                 .OrderBy(s => s.Category)
                 .ThenBy(s => s.DisplayName);

    /// <summary>
    /// Returns built-in schemas plus custom schemas for <paramref name="scope"/>.
    /// When <paramref name="scope"/> is null, app-scoped custom schemas are hidden.
    /// </summary>
    public IEnumerable<WireframeComponentSchema> GetAll(WireframeComponentScope? scope)
        => _index.Values
                 .Where(s => scope is null
                     ? s.ScopeAppId is null
                     : s.IsBuiltIn || (s.ScopeAppId is not null && scope.MatchesAppId(s.ScopeAppId)))
                 .OrderBy(s => s.Category)
                 .ThenBy(s => s.DisplayName);

    /// <summary>
    /// Returns schemas visible in <paramref name="scope"/> and allowed by document target packs.
    /// Null or empty target lists preserve legacy visibility.
    /// </summary>
    public IEnumerable<WireframeComponentSchema> GetAll(
        WireframeComponentScope? scope,
        IReadOnlyList<string>? targetPackIds)
        => GetAll(scope)
            .Where(s => WireframeComponentScope.IsVisibleInTargetPacks(s.ScopeAppId, s.IsBuiltIn, targetPackIds));

    /// <summary>Returns the schema for the given component type, or null if not registered.</summary>
    public WireframeComponentSchema? GetSchema(string type)
        => _index.TryGetValue(type, out var s) ? s : null;

    /// <summary>
    /// Returns the schema for <paramref name="type"/> in <paramref name="scope"/>, or null if not registered.
    /// Local custom names are resolved as <c>app:{id}:{type}</c>.
    /// </summary>
    public WireframeComponentSchema? GetSchema(string type, WireframeComponentScope? scope)
    {
        if (scope is null)
            return GetSchema(type);

        if (WireframeComponentScope.IsScopedType(type))
            return scope.ContainsType(type) ? GetSchema(type) : null;

        var scopedType = scope.NamespaceType(type);
        var scopedSchema = GetSchema(scopedType);
        if (scopedSchema is not null)
            return scopedSchema;

        var baselineSchema = GetSchema(type);
        return baselineSchema?.IsBuiltIn == true ? baselineSchema : null;
    }

    /// <summary>
    /// Returns the schema for <paramref name="type"/> when it is visible for the supplied target packs.
    /// </summary>
    public WireframeComponentSchema? GetSchema(
        string type,
        WireframeComponentScope? scope,
        IReadOnlyList<string>? targetPackIds)
    {
        var schema = GetSchema(type, scope);
        return schema is not null
               && WireframeComponentScope.IsVisibleInTargetPacks(schema.ScopeAppId, schema.IsBuiltIn, targetPackIds)
            ? schema
            : null;
    }

    /// <summary>Returns all schemas in a given category.</summary>
    public IEnumerable<WireframeComponentSchema> GetByCategory(string category)
        => GetByCategory(category, scope: null);

    /// <summary>Returns all schemas in a given category for <paramref name="scope"/>.</summary>
    public IEnumerable<WireframeComponentSchema> GetByCategory(string category, WireframeComponentScope? scope)
        => GetAll(scope).Where(s =>
               string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns all distinct category names in registration order.</summary>
    public IEnumerable<string> GetCategories()
        => GetCategories(scope: null);

    /// <summary>Returns all distinct category names in registration order for <paramref name="scope"/>.</summary>
    public IEnumerable<string> GetCategories(WireframeComponentScope? scope)
        => GetAll(scope)
                 .Select(s => s.Category)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .Order();

    private static WireframeComponentSchema NormalizeSchema(
        WireframeComponentSchema schema,
        string? sourceScopeAppId,
        bool isBuiltInSource)
    {
        var effectiveScopeAppId = ResolveScopeAppId(schema.Type, schema.ScopeAppId, sourceScopeAppId);
        var isBuiltIn = schema.IsBuiltIn || isBuiltInSource;

        if (effectiveScopeAppId is null)
        {
            return isBuiltIn == schema.IsBuiltIn
                ? schema
                : CopySchema(schema, schema.Type, scopeAppId: null, localType: schema.LocalType, isBuiltIn);
        }

        var scope = WireframeComponentScope.ForApp(effectiveScopeAppId);
        var localType = string.IsNullOrWhiteSpace(schema.LocalType)
            ? WireframeComponentScope.GetLocalType(schema.Type)
            : schema.LocalType.Trim();
        var normalizedType = scope.NamespaceType(localType);

        if (string.Equals(schema.Type, normalizedType, StringComparison.Ordinal)
            && string.Equals(schema.ScopeAppId, scope.AppId, StringComparison.Ordinal)
            && string.Equals(schema.LocalType, localType, StringComparison.Ordinal)
            && schema.IsBuiltIn == isBuiltIn)
        {
            return schema;
        }

        return CopySchema(schema, normalizedType, scope.AppId, localType, isBuiltIn);
    }

    private static WireframeComponentSchema CopySchema(
        WireframeComponentSchema schema,
        string type,
        string? scopeAppId,
        string? localType,
        bool isBuiltIn)
        => new()
        {
            Type = type,
            ScopeAppId = scopeAppId,
            LocalType = localType,
            Category = schema.Category,
            DisplayName = schema.DisplayName,
            Roles = schema.Roles,
            IsBuiltIn = isBuiltIn,
            DefaultWidth = schema.DefaultWidth,
            DefaultHeight = schema.DefaultHeight,
            Props = schema.Props,
            SizePresets = schema.SizePresets,
        };

    private static string? ResolveScopeAppId(string type, string? schemaScopeAppId, string? sourceScopeAppId)
    {
        if (!string.IsNullOrWhiteSpace(sourceScopeAppId))
            return sourceScopeAppId;

        if (!string.IsNullOrWhiteSpace(schemaScopeAppId))
            return schemaScopeAppId;

        return WireframeComponentScope.TryGetAppId(type, out var parsedAppId)
            ? parsedAppId
            : null;
    }
}
