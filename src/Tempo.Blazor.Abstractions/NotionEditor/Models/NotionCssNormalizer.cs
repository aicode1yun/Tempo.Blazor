using System.Text.RegularExpressions;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Dependency-free normalization for the narrow CSS color surface used by Notion content.</summary>
public static partial class NotionCssNormalizer
{
    private static readonly HashSet<string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "transparent",
        "black",
        "silver",
        "gray",
        "white",
        "maroon",
        "red",
        "purple",
        "fuchsia",
        "green",
        "lime",
        "olive",
        "yellow",
        "navy",
        "blue",
        "teal",
        "aqua",
        "orange"
    };

    /// <summary>
    /// Validates and normalizes one literal CSS color. Functions with external state, declarations,
    /// custom properties, quotes and statement separators are rejected.
    /// </summary>
    public static bool TryNormalizeColor(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        if (candidate.Length > NotionAuthoringLimits.MaxCssColorLength ||
            candidate.IndexOfAny([';', '"', '\'', '<', '>', '\\']) >= 0 ||
            candidate.Contains("url(", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("var(", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("expression(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HexColorRegex().IsMatch(candidate))
        {
            normalized = candidate.ToLowerInvariant();
            return true;
        }

        if (NamedColorRegex().IsMatch(candidate) && NamedColors.Contains(candidate))
        {
            normalized = candidate.ToLowerInvariant();
            return true;
        }

        if (FunctionalColorRegex().IsMatch(candidate) && HasBalancedParentheses(candidate))
        {
            normalized = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a style containing only <c>color</c> and <c>background-color</c>
    /// declarations with literal color values.
    /// </summary>
    public static bool TryNormalizeColorStyle(string? style, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(style))
        {
            return true;
        }

        var declarations = style.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (declarations.Length == 0)
        {
            return false;
        }

        var safe = new List<string>(declarations.Length);
        foreach (var declaration in declarations)
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1)
            {
                return false;
            }

            var property = declaration[..separator].Trim().ToLowerInvariant();
            if (property is not "color" and not "background-color")
            {
                return false;
            }

            var valuePart = declaration[(separator + 1)..];
            if (!TryNormalizeColor(valuePart, out var color) ||
                color is null)
            {
                return false;
            }

            safe.Add(valuePart.Length > 0 && char.IsWhiteSpace(valuePart[0])
                ? $"{property}: {color}"
                : $"{property}:{color}");
        }

        normalized = string.Join("; ", safe);
        return true;
    }

    private static bool HasBalancedParentheses(string value)
    {
        var depth = 0;
        foreach (var character in value)
        {
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && --depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    [GeneratedRegex(@"^#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{1}|[0-9a-fA-F]{3}|[0-9a-fA-F]{5})?$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"^[a-zA-Z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamedColorRegex();

    [GeneratedRegex(@"^(?:rgb|rgba|hsl|hsla)\(\s*[0-9.+,%/\s-]+\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionalColorRegex();
}
