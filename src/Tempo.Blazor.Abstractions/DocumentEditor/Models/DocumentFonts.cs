namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Represents a font family available to the document editor.</summary>
public class DocumentFontFamily
{
    /// <summary>Stable key used in serialized documents (e.g. "arial").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in font pickers.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>CSS font-family stack value.</summary>
    public string CssFamily { get; set; } = string.Empty;

    /// <summary>Whether this is the default fallback font.</summary>
    public bool IsFallback { get; set; }
}

/// <summary>Optional filter used when querying font families.</summary>
public class DocumentFontQuery
{
    /// <summary>Optional search term to filter font families by display name or key.</summary>
    public string? Search { get; set; }

    /// <summary>Maximum number of results to return. Null means no limit.</summary>
    public int? Take { get; set; }

    /// <summary>Optional document id used to scope font availability to a specific document.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional BCP-47 culture name used to filter locale-appropriate fonts.</summary>
    public string? CultureName { get; set; }
}
