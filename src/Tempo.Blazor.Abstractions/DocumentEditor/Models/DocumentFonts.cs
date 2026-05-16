namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Query used when resolving document editor font families.</summary>
public sealed class DocumentFontQuery
{
    /// <summary>Optional document identifier for tenant- or document-specific font catalogs.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional culture name used to localize font display names.</summary>
    public string? CultureName { get; set; }
}

/// <summary>Font family exposed by a document editor font provider.</summary>
public sealed class DocumentFontFamily
{
    /// <summary>Stable provider key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>User-visible font family name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>CSS font-family value safe to apply in the editor.</summary>
    public string CssFamily { get; set; } = string.Empty;

    /// <summary>Whether this family is the provider fallback.</summary>
    public bool IsFallback { get; set; }
}
