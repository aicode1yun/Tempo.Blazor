using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>
/// Sanitizes the inline HTML allowed inside text-bearing blocks: only a small safe whitelist of tags
/// and attributes survives; scripts/styles are dropped with their content, event handlers and unsafe
/// URL schemes are stripped. Built on AngleSharp's HTML parser so it is robust against evasion.
/// </summary>
public static class HtmlContentSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        { "b", "i", "u", "strong", "em", "a", "br", "span", "p", "ul", "ol", "li" };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
        { "href", "style", "target", "rel" };

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
        { "http", "https", "mailto" };

    private static readonly HashSet<string> DroppedWithContent = new(StringComparer.OrdinalIgnoreCase)
        { "script", "style", "iframe", "object", "embed", "link", "meta", "base", "title" };

    private static readonly HtmlParser Parser = new();

    /// <summary>Returns a sanitized copy of <paramref name="html"/> safe to embed in generated markup.</summary>
    public static string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var document = Parser.ParseDocument("<!DOCTYPE html><html><body></body></html>");
        document.Body!.InnerHtml = html;
        SanitizeChildren(document.Body);
        return document.Body.InnerHtml;
    }

    private static void SanitizeChildren(INode parent)
    {
        foreach (var node in parent.ChildNodes.ToArray())
        {
            switch (node)
            {
                case IComment:
                    node.RemoveFromParent();
                    break;
                case IElement element:
                    SanitizeElement(parent, element);
                    break;
            }
        }
    }

    private static void SanitizeElement(INode parent, IElement element)
    {
        if (DroppedWithContent.Contains(element.LocalName))
        {
            element.Remove();
            return;
        }

        foreach (var attribute in element.Attributes.ToArray())
        {
            if (!IsAttributeAllowed(element, attribute))
                element.RemoveAttribute(attribute.NamespaceUri, attribute.Name);
        }

        SanitizeChildren(element);

        if (!AllowedTags.Contains(element.LocalName))
        {
            // Unwrap unknown formatting tags: keep their (already-sanitized) children.
            while (element.FirstChild is { } child)
                parent.InsertBefore(child, element);
            element.Remove();
        }
    }

    private static bool IsAttributeAllowed(IElement element, IAttr attribute)
    {
        var name = attribute.Name;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) return false;
        if (!AllowedAttributes.Contains(name)) return false;

        if (name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("src", StringComparison.OrdinalIgnoreCase))
            return IsSafeUrl(attribute.Value);

        if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
            return IsSafeStyle(attribute.Value);

        return true;
    }

    private static bool IsSafeUrl(string value)
    {
        var trimmed = value.TrimStart();
        var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) return true; // relative URL (no scheme)
        // A scheme contains only letters/digits/+-.; anything else before ':' is not a scheme (e.g. a path).
        var scheme = trimmed[..colon];
        if (scheme.Any(c => !char.IsLetterOrDigit(c) && c is not ('+' or '-' or '.'))) return true;
        return AllowedSchemes.Contains(scheme);
    }

    private static bool IsSafeStyle(string value)
    {
        var lowered = value.ToLowerInvariant();
        return !lowered.Contains("javascript:", StringComparison.Ordinal) &&
            !lowered.Contains("expression(", StringComparison.Ordinal) &&
            !lowered.Contains("url(", StringComparison.Ordinal);
    }
}
