namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Helpers for validating and normalizing hyperlink hrefs in documents.</summary>
public static class DocumentLinkUtility
{
    private static readonly HashSet<string> SafeSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "tel" };

    /// <summary>Returns true when the href uses a known safe URI scheme.</summary>
    public static bool IsSafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri))
        {
            return SafeSchemes.Contains(uri.Scheme);
        }

        // Allow relative URLs (no scheme)
        return !href.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Trims whitespace and ensures the href is a usable value.</summary>
    public static string NormalizeHref(string? href)
    {
        var trimmed = href?.Trim() ?? string.Empty;

        // Reject javascript: hrefs silently by returning empty
        if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // Prepend https:// when no scheme is present and the value looks like a domain
        if (trimmed.Length > 0
            && !trimmed.Contains("://")
            && !trimmed.StartsWith('/'))
        {
            trimmed = "https://" + trimmed;
        }

        return trimmed;
    }
}
