namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Token and icon layers available to one stencil render pass.</summary>
internal sealed class StencilTokenScope
{
    public static StencilTokenScope Empty { get; } = new();

    public StencilTokenScope(
        IReadOnlyDictionary<string, string>? elementOverrides = null,
        IReadOnlyDictionary<string, string>? documentTheme = null,
        IReadOnlyDictionary<string, string>? packTheme = null,
        IReadOnlyDictionary<string, string>? packDefaults = null,
        IReadOnlyDictionary<string, string>? packIcons = null)
    {
        ElementOverrides = Copy(elementOverrides);
        DocumentTheme = Copy(documentTheme);
        PackTheme = Copy(packTheme);
        PackDefaults = Copy(packDefaults);
        PackIcons = Copy(packIcons);
    }

    public IReadOnlyDictionary<string, string> ElementOverrides { get; }

    public IReadOnlyDictionary<string, string> DocumentTheme { get; }

    public IReadOnlyDictionary<string, string> PackTheme { get; }

    public IReadOnlyDictionary<string, string> PackDefaults { get; }

    public IReadOnlyDictionary<string, string> PackIcons { get; }

    public static StencilTokenScope FromPack(StencilPack pack, string? themeName = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var packTheme = !string.IsNullOrWhiteSpace(themeName)
                        && pack.Themes.TryGetValue(themeName, out var theme)
            ? theme
            : null;

        return new StencilTokenScope(
            packTheme: packTheme,
            packDefaults: pack.Tokens,
            packIcons: pack.Icons);
    }

    internal StencilTokenResolver CreateResolver()
        => new(ElementOverrides, DocumentTheme, PackTheme, PackDefaults);

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? source)
        => source is null || source.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(source, StringComparer.Ordinal);
}
