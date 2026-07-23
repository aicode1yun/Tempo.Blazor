using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Dependency-free HTML sanitizer for Notion rich-text fragments and renderer content.</summary>
public static partial class NotionHtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "strong", "b", "em", "i", "u", "s", "code", "br", "a"
    };

    private static readonly HashSet<string> AllowedBlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "mark"
    };

    private static readonly HashSet<string> AllowedClasses = new(StringComparer.Ordinal)
    {
        "tm-notion-inline-math",
        "tm-notion-comment-highlight",
        "tm-notion-status",
        "tm-notion-status__label",
        "tm-notion-mention",
        "tm-notion-smart-link"
    };

    private static readonly string[] AllowedClassPrefixes =
    [
        "tm-notion-status--",
        "tm-notion-mention--"
    ];

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

    /// <summary>Sanitizes a narrow table/export HTML fragment.</summary>
    public static string SanitizeHtmlFragment(string? html)
        => SanitizeCore(html, allowEditorChrome: false);

    /// <summary>Sanitizes stored editor content, preserving only known editor chrome.</summary>
    public static string SanitizeBlockContent(string? html)
        => SanitizeCore(html, allowEditorChrome: true);

    /// <summary>
    /// Returns whether a table-cell fragment already belongs to the strict authoring profile.
    /// The normalized representation is returned for deterministic persistence.
    /// </summary>
    public static bool TryNormalizeTableCellHtml(string? html, out string normalized)
    {
        normalized = SanitizeHtmlFragment(html);
        return string.Equals(html ?? string.Empty, normalized, StringComparison.Ordinal);
    }

    /// <summary>HTML-encodes plain text and converts line endings to <c>&lt;br&gt;</c>.</summary>
    public static string EncodePlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return HtmlEncoder.Default.Encode(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    /// <summary>Returns whether a hyperlink scheme is allowed in Notion rich text.</summary>
    public static bool IsSafeHref(string? href)
        => !string.IsNullOrWhiteSpace(href) &&
           Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" or "mailto";

    private static string SanitizeCore(string? html, bool allowEditorChrome)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var bounded = html.Length <= NotionAuthoringLimits.MaxCellHtmlLength
            ? html
            : html[..NotionAuthoringLimits.MaxCellHtmlLength];
        try
        {
            var withoutUnsafeBlocks = UnsafeBlockRegex().Replace(bounded, string.Empty);
            return SanitizeTags(withoutUnsafeBlocks, allowEditorChrome);
        }
        catch (RegexMatchTimeoutException)
        {
            return HtmlEncoder.Default.Encode(bounded);
        }
    }

    private static string SanitizeTags(string html, bool allowEditorChrome)
    {
        var builder = new StringBuilder(html.Length);
        var cursor = 0;
        while (cursor < html.Length)
        {
            var open = html.IndexOf('<', cursor);
            if (open < 0)
            {
                AppendText(builder, html.AsSpan(cursor));
                break;
            }

            AppendText(builder, html.AsSpan(cursor, open - cursor));
            var close = html.IndexOf('>', open + 1);
            if (close < 0)
            {
                builder.Append("&lt;");
                AppendText(builder, html.AsSpan(open + 1));
                break;
            }

            var token = html[open..(close + 1)];
            var match = TagRegex().Match(token);
            if (match.Success && match.Index == 0 && match.Length == token.Length)
            {
                builder.Append(SanitizeTag(match, allowEditorChrome));
            }
            else
            {
                builder.Append("&lt;");
                AppendText(builder, token.AsSpan(1, token.Length - 2));
                builder.Append("&gt;");
            }

            cursor = close + 1;
        }

        return builder.ToString();
    }

    private static string SanitizeTag(Match match, bool allowEditorChrome)
    {
        var isClosing = match.Groups["slash"].Value.Length > 0;
        var name = match.Groups["name"].Value;
        var attrs = match.Groups["attrs"].Value;
        var isBlockTag = allowEditorChrome && AllowedBlockTags.Contains(name);
        if (!AllowedTags.Contains(name) && !isBlockTag)
        {
            return string.Empty;
        }

        if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            return "<br>";
        }

        var normalizedName = NormalizeTagName(name);
        if (isClosing)
        {
            return $"</{normalizedName}>";
        }

        if (name.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            var href = HrefRegex().Match(match.Value);
            return href.Success && IsSafeHref(href.Groups["href"].Value)
                ? $"<a href=\"{HtmlEncoder.Default.Encode(href.Groups["href"].Value)}\" rel=\"noopener noreferrer\">"
                : "<a>";
        }

        return isBlockTag
            ? $"<{normalizedName}{BuildSafeAttributes(attrs)}>"
            : $"<{normalizedName}>";
    }

    private static string BuildSafeAttributes(string attrs)
    {
        var builder = new StringBuilder();
        var attributes = AttributeRegex().Matches(attrs);
        var classes = attributes
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
                builder.Append(" class=\"")
                    .Append(HtmlEncoder.Default.Encode(string.Join(' ', kept)))
                    .Append('"');
            }
        }

        var style = attributes
            .Where(attribute => attribute.Groups["key"].Value.Equals("style", StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Groups["value"].Value)
            .FirstOrDefault();
        if (NotionCssNormalizer.TryNormalizeColorStyle(style, out var safeStyle) &&
            safeStyle is not null)
        {
            builder.Append(" style=\"").Append(safeStyle).Append('"');
        }

        foreach (Match attribute in attributes)
        {
            var key = attribute.Groups["key"].Value;
            var value = attribute.Groups["value"].Value;
            var allowed =
                AllowedDataAttributes.Contains(key) ||
                (key.Equals("contenteditable", StringComparison.OrdinalIgnoreCase) &&
                 value.Equals("false", StringComparison.OrdinalIgnoreCase)) ||
                key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("aria-label", StringComparison.OrdinalIgnoreCase);
            if (allowed)
            {
                builder.Append(' ')
                    .Append(key.ToLowerInvariant())
                    .Append("=\"")
                    .Append(HtmlEncoder.Default.Encode(value))
                    .Append('"');
            }
        }

        return builder.ToString();
    }

    private static bool IsAllowedClass(string name)
        => AllowedClasses.Contains(name) ||
           AllowedClassPrefixes.Any(prefix =>
               name.StartsWith(prefix, StringComparison.Ordinal) &&
               name.Length > prefix.Length);

    private static void AppendText(StringBuilder builder, ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                _ => character
            });
        }
    }

    private static string NormalizeTagName(string tag) => tag.ToLowerInvariant() switch
    {
        "b" => "strong",
        "i" => "em",
        _ => tag.ToLowerInvariant()
    };

    [GeneratedRegex(@"<(?<slash>/)?\s*(?<name>[a-zA-Z0-9]+)(?<attrs>[^>]*)>", RegexOptions.CultureInvariant, 250)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s(?:href)\s*=\s*[""'](?<href>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"<(script|style|iframe|object|embed)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, 250)]
    private static partial Regex UnsafeBlockRegex();

    [GeneratedRegex("""(?<key>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.CultureInvariant, 250)]
    private static partial Regex AttributeRegex();

}
