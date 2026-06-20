namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Utility methods for document editor hyperlink validation.</summary>
public static class DocumentLinkUtility
{
    /// <summary>Determines whether a hyperlink target is safe to persist or render.</summary>
    public static bool IsSafeHref(string? href)
    {
        var value = NormalizeHref(href);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto" or "tel";
    }

    /// <summary>Normalizes a hyperlink target before it is persisted.</summary>
    public static string NormalizeHref(string? href)
        => string.IsNullOrWhiteSpace(href) ? string.Empty : href.Trim();
}
