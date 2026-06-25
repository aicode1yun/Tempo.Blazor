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
}
