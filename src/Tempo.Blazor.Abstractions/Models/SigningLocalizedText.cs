using System.Globalization;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Localized text with a default fallback and culture-specific translations.</summary>
public class SigningLocalizedText
{
    /// <summary>Default text used when no culture-specific translation matches.</summary>
    public string? Default { get; set; }

    /// <summary>Translations keyed by culture name, for example <c>en</c>, <c>cs</c>, or <c>cs-CZ</c>.</summary>
    public Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves the best matching text for the requested culture.</summary>
    /// <param name="culture">Requested culture name.</param>
    /// <param name="fallbackCulture">Fallback culture name, usually the template language.</param>
    /// <param name="legacyFallback">Legacy text used when no localized value is configured.</param>
    /// <returns>The resolved text or an empty string.</returns>
    public string Resolve(string? culture = null, string? fallbackCulture = null, string? legacyFallback = null)
    {
        return SigningLocalizationResolver.ResolveText(this, culture, fallbackCulture, legacyFallback);
    }

    internal static IEnumerable<string> ExpandCultureCandidates(string? culture)
    {
        var normalized = NormalizeCulture(culture);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        yield return normalized;

        var neutral = GetNeutralCultureName(normalized);
        if (!string.IsNullOrWhiteSpace(neutral) && !string.Equals(neutral, normalized, StringComparison.OrdinalIgnoreCase))
        {
            yield return neutral;
        }
    }

    private static string? NormalizeCulture(string? culture)
    {
        var trimmed = culture?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(trimmed).Name;
        }
        catch (CultureNotFoundException)
        {
            return trimmed;
        }
    }

    private static string? GetNeutralCultureName(string culture)
    {
        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(culture);
            return cultureInfo.IsNeutralCulture
                ? cultureInfo.Name
                : cultureInfo.Parent.Name;
        }
        catch (CultureNotFoundException)
        {
            var separator = culture.IndexOf('-', StringComparison.Ordinal);
            return separator > 0 ? culture[..separator] : culture;
        }
    }
}
