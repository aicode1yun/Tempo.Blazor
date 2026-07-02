namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>
/// Resolves a token key through element override, document theme, pack theme, pack defaults,
/// and finally a literal fallback. Keys are matched with <see cref="StringComparer.Ordinal"/>.
/// </summary>
public sealed class StencilTokenResolver
{
    private readonly IReadOnlyDictionary<string, string> _elementOverrides;
    private readonly IReadOnlyDictionary<string, string> _documentTheme;
    private readonly IReadOnlyDictionary<string, string> _packTheme;
    private readonly IReadOnlyDictionary<string, string> _packDefaults;

    public StencilTokenResolver(
        IReadOnlyDictionary<string, string>? elementOverrides,
        IReadOnlyDictionary<string, string>? documentTheme,
        IReadOnlyDictionary<string, string>? packTheme,
        IReadOnlyDictionary<string, string>? packDefaults)
    {
        _elementOverrides = Copy(elementOverrides);
        _documentTheme = Copy(documentTheme);
        _packTheme = Copy(packTheme);
        _packDefaults = Copy(packDefaults);
    }

    /// <summary>First matching layer wins; returns <paramref name="literalFallback"/> or empty string if none; never throws.</summary>
    public string Resolve(string key, string? literalFallback = null)
    {
        if (string.IsNullOrEmpty(key))
            return literalFallback ?? string.Empty;

        if (_elementOverrides.TryGetValue(key, out var elementValue))
            return elementValue;

        if (_documentTheme.TryGetValue(key, out var documentValue))
            return documentValue;

        if (_packTheme.TryGetValue(key, out var packThemeValue))
            return packThemeValue;

        if (_packDefaults.TryGetValue(key, out var packDefaultValue))
            return packDefaultValue;

        return literalFallback ?? string.Empty;
    }

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? source)
        => source is null || source.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(source, StringComparer.Ordinal);
}
