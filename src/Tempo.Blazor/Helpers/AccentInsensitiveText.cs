using System.Globalization;
using System.Text;

namespace Tempo.Blazor.Helpers;

/// <summary>
/// Accent-insensitive text matching used by filterable components (TmFilterableDropdown,
/// TmMultiColumnComboBox). Normalizes both sides to Unicode FormD and strips combining
/// diacritical marks, so e.g. "usti" matches "Ústí" and "práha" matches "Praha".
/// </summary>
internal static class AccentInsensitiveText
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="source"/> contains <paramref name="term"/>
    /// ignoring case and diacritics. An empty term matches everything.
    /// </summary>
    public static bool Contains(string source, string term)
    {
        if (string.IsNullOrEmpty(term)) return true;
        if (string.IsNullOrEmpty(source)) return false;
        return RemoveDiacritics(source).Contains(RemoveDiacritics(term), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strips combining diacritical marks via FormD normalization ("Ústí" → "Usti").
    /// The result is re-composed to FormC so lengths stay comparable for plain text.
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
