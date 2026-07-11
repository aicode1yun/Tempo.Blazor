namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Translates the code block's human-readable language names into the grammar ids Prism uses.
/// Most names only need lowercasing; the ones that do not are listed explicitly, because Prism
/// silently highlights nothing when it is handed an id it does not know.
/// </summary>
internal static class NotionCodeLanguage
{
    private static readonly Dictionary<string, string?> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Plain Text"] = null,
        ["C#"]         = "csharp",
        ["C++"]        = "cpp",
        ["Shell"]      = "bash",
        ["HTML"]       = "markup",
        ["XML"]        = "markup"
    };

    /// <summary>The Prism grammar id, or <c>null</c> when the block should not be highlighted.</summary>
    public static string? ToPrismId(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;

        var name = language.Trim();
        return Aliases.TryGetValue(name, out var alias)
            ? alias
            : name.ToLowerInvariant();
    }
}
