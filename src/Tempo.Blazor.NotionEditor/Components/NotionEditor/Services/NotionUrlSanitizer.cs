using System.Text;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Decides whether a URL coming from imported Markdown or HTML may be emitted into
/// an <c>href</c> / <c>src</c> attribute. Only http, https and mailto are allowed;
/// scheme-relative and relative URLs are permitted because they cannot execute script.
/// </summary>
internal static partial class NotionUrlSanitizer
{
    private static readonly HashSet<string> AllowedSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" };

    /// <summary>Returns true when the URL is safe to place in an href or src attribute.</summary>
    public static bool IsSafe(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var probe = Normalize(url);
        if (probe.Length == 0)
        {
            return false;
        }

        var colon = probe.IndexOf(':');
        if (colon <= 0)
        {
            // No scheme at all — a relative URL such as "/page/1" or "#anchor".
            return true;
        }

        // A colon appearing after a path separator is part of the path, not a scheme.
        var separator = probe.IndexOfAny(['/', '?', '#']);
        if (separator >= 0 && separator < colon)
        {
            return true;
        }

        var scheme = probe[..colon];
        return SchemeRegex().IsMatch(scheme) && AllowedSchemes.Contains(scheme);
    }

    /// <summary>
    /// Strips characters a browser ignores when resolving a scheme (control chars, whitespace)
    /// and decodes entities, so that "java&#115;cript:" or "java\tscript:" cannot slip through.
    /// </summary>
    private static string Normalize(string url)
    {
        var decoded = DecodeEntities(url);
        var builder = new StringBuilder(decoded.Length);
        foreach (var ch in decoded)
        {
            if (ch > ' ' && ch != '\u007F')
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string DecodeEntities(string value)
    {
        if (!value.Contains('&'))
        {
            return value;
        }

        var decoded = NumericEntityRegex().Replace(value, match =>
        {
            var digits = match.Groups["dec"].Success ? match.Groups["dec"].Value : match.Groups["hex"].Value;
            var fromHex = match.Groups["hex"].Success;
            try
            {
                var code = Convert.ToInt32(digits, fromHex ? 16 : 10);
                return code is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(code) : string.Empty;
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        });

        return decoded
            .Replace("&colon;", ":", StringComparison.OrdinalIgnoreCase)
            .Replace("&tab;", "\t", StringComparison.OrdinalIgnoreCase)
            .Replace("&newline;", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("""^[a-zA-Z][a-zA-Z0-9+.\-]*$""")]
    private static partial Regex SchemeRegex();

    [GeneratedRegex("""&#(?:(?<dec>[0-9]{1,7})|[xX](?<hex>[0-9a-fA-F]{1,6}));?""")]
    private static partial Regex NumericEntityRegex();
}
