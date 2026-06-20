namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Resolves localized signing texts with deterministic culture fallback.</summary>
public static class SigningLocalizationResolver
{
    /// <summary>Resolves localized text using requested culture, fallback culture, default text, and legacy fallback.</summary>
    public static string ResolveText(SigningLocalizedText? text, string? culture, string? fallbackCulture = null, string? legacyFallback = null, string? finalFallback = null)
    {
        foreach (var candidate in GetCultureCandidates(culture, fallbackCulture))
        {
            var translation = FindTranslation(text, candidate);
            if (!string.IsNullOrWhiteSpace(translation))
            {
                return translation;
            }
        }

        if (!string.IsNullOrWhiteSpace(text?.Default))
        {
            return text.Default!;
        }

        if (!string.IsNullOrWhiteSpace(legacyFallback))
        {
            return legacyFallback!;
        }

        return finalFallback ?? string.Empty;
    }

    /// <summary>Resolves a signing field label.</summary>
    public static string ResolveFieldLabel(SigningField? field, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return field is null
            ? finalFallback ?? string.Empty
            : ResolveText(field.Labels, culture, fallbackCulture, field.Title ?? field.Name, finalFallback);
    }

    /// <summary>Resolves a signing field title.</summary>
    public static string ResolveFieldTitle(SigningField? field, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return field is null
            ? finalFallback ?? string.Empty
            : ResolveText(field.Titles, culture, fallbackCulture, field.Title ?? field.Name, finalFallback);
    }

    /// <summary>Resolves a signing field description.</summary>
    public static string ResolveFieldDescription(SigningField? field, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return field is null
            ? finalFallback ?? string.Empty
            : ResolveText(field.Descriptions, culture, fallbackCulture, field.Description, finalFallback);
    }

    /// <summary>Resolves a signing field placeholder.</summary>
    public static string ResolveFieldPlaceholder(SigningField? field, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return field is null
            ? finalFallback ?? string.Empty
            : ResolveText(field.Placeholders, culture, fallbackCulture, null, finalFallback);
    }

    /// <summary>Resolves a choice option display label without changing the stable option value.</summary>
    public static string ResolveOptionLabel(SigningFieldOption? option, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return option is null
            ? finalFallback ?? string.Empty
            : ResolveText(option.Labels, culture, fallbackCulture, option.Value, finalFallback);
    }

    /// <summary>Resolves a validation message.</summary>
    public static string ResolveValidationMessage(SigningFieldValidation? validation, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return validation is null
            ? finalFallback ?? string.Empty
            : ResolveText(validation.Messages, culture, fallbackCulture, validation.Message, finalFallback);
    }

    /// <summary>Resolves a document page label.</summary>
    public static string ResolvePageLabel(SigningDocumentPage? page, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return page is null
            ? finalFallback ?? string.Empty
            : ResolveText(page.Labels, culture, fallbackCulture, page.Label, finalFallback);
    }

    /// <summary>Resolves a submitter role display name.</summary>
    public static string ResolveRoleName(SigningSubmitterRole? role, string? culture, string? fallbackCulture = null, string? finalFallback = null)
    {
        return role is null
            ? finalFallback ?? string.Empty
            : ResolveText(role.Labels, culture, fallbackCulture, role.Name, finalFallback);
    }

    private static IEnumerable<string> GetCultureCandidates(string? culture, string? fallbackCulture)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in SigningLocalizedText.ExpandCultureCandidates(culture)
                     .Concat(SigningLocalizedText.ExpandCultureCandidates(fallbackCulture)))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? FindTranslation(SigningLocalizedText? text, string culture)
    {
        if (text?.Translations is null || text.Translations.Count == 0)
        {
            return null;
        }

        foreach (var pair in text.Translations)
        {
            if (string.Equals(pair.Key?.Trim(), culture, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
