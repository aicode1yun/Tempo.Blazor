using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static partial class NotionInlineHtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "strong", "b", "em", "i", "u", "s", "code", "br", "a"
    };

    public static string SanitizeHtmlFragment(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutUnsafeBlocks = UnsafeBlockRegex().Replace(html, string.Empty);
        return TagRegex().Replace(withoutUnsafeBlocks, match =>
        {
            var slash = match.Groups["slash"].Value;
            var name = match.Groups["name"].Value;
            if (!AllowedTags.Contains(name))
            {
                return string.Empty;
            }

            if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                return "<br>";
            }

            if (slash.Length > 0)
            {
                return $"</{NormalizeTagName(name)}>";
            }

            if (!name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                return $"<{NormalizeTagName(name)}>";
            }

            var href = HrefRegex().Match(match.Value);
            if (!href.Success || !IsSafeHref(href.Groups["href"].Value))
            {
                return "<a>";
            }

            return $"<a href=\"{HtmlEncoder.Default.Encode(href.Groups["href"].Value)}\" rel=\"noopener noreferrer\">";
        });
    }

    /// <summary>
    /// Tags the editor may store inside a block, on top of <see cref="AllowedTags"/>.
    /// They carry the editor's own inline chrome and would be destroyed by the narrow
    /// fragment profile used by <see cref="SanitizeHtmlFragment"/>.
    /// </summary>
    private static readonly HashSet<string> AllowedBlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "mark"
    };

    /// <summary>Class names the editor generates. Anything else is dropped, so an attacker cannot restyle the page.</summary>
    private static readonly HashSet<string> AllowedClasses = new(StringComparer.Ordinal)
    {
        "tm-notion-inline-math",
        "tm-notion-comment-highlight",
        "tm-notion-status",
        "tm-notion-status__label",
        "tm-notion-mention",
        "tm-notion-smart-link"
    };

    /// <summary>Class prefixes with a free-form modifier, e.g. tm-notion-status--green.</summary>
    private static readonly string[] AllowedClassPrefixes =
    [
        "tm-notion-status--",
        "tm-notion-mention--"
    ];

    /// <summary>Data attributes the editor reads back. Everything else, including every on* handler, is dropped.</summary>
    private static readonly HashSet<string> AllowedDataAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data-expr",
        "data-comment-id",
        "data-block-id",
        "data-status-label",
        "data-status-color",
        "data-type",
        "data-id",
        "data-href"
    };

    /// <summary>
    /// Sanitizes the HTML stored in a core block (paragraph, heading, callout, quote, list, todo).
    /// Block content is written into the DOM with innerHTML, so an <c>onerror</c> payload would run on render.
    /// Unlike <see cref="SanitizeHtmlFragment"/> this profile keeps the editor's own inline chrome —
    /// status chips, mentions, inline math and comment highlights — by allowing span and mark with a
    /// whitelisted class and a whitelisted set of data attributes.
    /// </summary>
    public static string SanitizeBlockContent(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutUnsafeBlocks = UnsafeBlockRegex().Replace(html, string.Empty);

        return TagRegex().Replace(withoutUnsafeBlocks, match =>
        {
            var isClosing = match.Groups["slash"].Value.Length > 0;
            var name = match.Groups["name"].Value;
            var attrs = match.Groups["attrs"].Value;

            var isBlockTag = AllowedBlockTags.Contains(name);
            if (!AllowedTags.Contains(name) && !isBlockTag)
            {
                return string.Empty;
            }

            if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                return "<br>";
            }

            var normalized = NormalizeTagName(name);
            if (isClosing)
            {
                return $"</{normalized}>";
            }

            if (name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                var href = HrefRegex().Match(match.Value);
                return href.Success && IsSafeHref(href.Groups["href"].Value)
                    ? $"<a href=\"{HtmlEncoder.Default.Encode(href.Groups["href"].Value)}\" rel=\"noopener noreferrer\">"
                    : "<a>";
            }

            return isBlockTag
                ? $"<{normalized}{BuildSafeAttributes(attrs)}>"
                : $"<{normalized}>";
        });
    }

    /// <summary>Rebuilds only the attributes an editor chip is allowed to carry; the rest is discarded.</summary>
    private static string BuildSafeAttributes(string attrs)
    {
        var builder = new StringBuilder();

        var classes = AttributeRegex().Matches(attrs)
            .Where(attribute => attribute.Groups["key"].Value.Equals("class", StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Groups["value"].Value)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(classes))
        {
            var kept = classes
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsAllowedClass)
                .ToList();

            if (kept.Count > 0)
            {
                builder.Append(" class=\"").Append(HtmlEncoder.Default.Encode(string.Join(' ', kept))).Append('"');
            }
        }

        var style = AttributeRegex().Matches(attrs)
            .Where(attribute => attribute.Groups["key"].Value.Equals("style", StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Groups["value"].Value)
            .FirstOrDefault();

        if (SafeColorStyle(style) is { } safeStyle)
        {
            // Emitted raw: SafeColorStyle already rejects everything but literal colour values,
            // so there is no quote or angle bracket left to escape — and encoding "rgb(0,0,0)"
            // would turn the parentheses into entities the browser will not parse as CSS.
            builder.Append(" style=\"").Append(safeStyle).Append('"');
        }

        foreach (Match attribute in AttributeRegex().Matches(attrs))
        {
            var key = attribute.Groups["key"].Value;
            var value = attribute.Groups["value"].Value;

            var allowed =
                AllowedDataAttributes.Contains(key) ||
                (key.Equals("contenteditable", StringComparison.OrdinalIgnoreCase) && value.Equals("false", StringComparison.OrdinalIgnoreCase)) ||
                key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("aria-label", StringComparison.OrdinalIgnoreCase);

            if (allowed)
            {
                builder.Append(' ').Append(key.ToLowerInvariant())
                    .Append("=\"").Append(HtmlEncoder.Default.Encode(value)).Append('"');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The colour picker writes <c>span[style]</c>, so the style attribute has to survive. It may
    /// only carry <c>color</c> and <c>background-color</c> with a literal colour value: a single
    /// stray declaration — a <c>url()</c>, a <c>position: fixed</c> overlay — drops the whole
    /// attribute. Returns <c>null</c> when nothing may be kept.
    /// </summary>
    private static string? SafeColorStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return null;

        var declarations = style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (declarations.Length == 0) return null;

        foreach (var declaration in declarations)
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0) return null;

            var property = declaration[..separator].Trim();
            var value = declaration[(separator + 1)..].Trim();

            if (!property.Equals("color", StringComparison.OrdinalIgnoreCase) &&
                !property.Equals("background-color", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!ColorValueRegex().IsMatch(value)) return null;
        }

        return string.Join("; ", declarations);
    }

    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|[a-zA-Z]+|(?:rgb|rgba|hsl|hsla)\(\s*[0-9.,%/\s]+\))$")]
    private static partial Regex ColorValueRegex();

    private static bool IsAllowedClass(string name) =>
        AllowedClasses.Contains(name) ||
        AllowedClassPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length);

    public static string EncodePlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return HtmlEncoder.Default.Encode(text).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static bool IsSafeHref(string href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" or "mailto";

    private static string NormalizeTagName(string tag) => tag.ToLowerInvariant() switch
    {
        "b" => "strong",
        "i" => "em",
        _ => tag.ToLowerInvariant()
    };

    [GeneratedRegex(@"<(?<slash>/)?\s*(?<name>[a-zA-Z0-9]+)(?<attrs>[^>]*)>", RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s(?:href)\s*=\s*[""'](?<href>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"<(script|style|iframe|object|embed)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex UnsafeBlockRegex();

    [GeneratedRegex("""(?<key>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.Compiled)]
    private static partial Regex AttributeRegex();
}
