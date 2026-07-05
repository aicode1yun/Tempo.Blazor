namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Library-agnostic UI role that describes a component concept such as
/// <c>search-input</c>, <c>data-table</c>, or <c>signature-pad</c>.
/// </summary>
public sealed class UiRoleDefinition
{
    /// <summary>Creates a validated UI role definition.</summary>
    public UiRoleDefinition(
        string slug,
        string displayName,
        string definition,
        IEnumerable<string>? synonyms = null)
    {
        if (!IsKebabCaseSlug(slug))
            throw new ArgumentException("Value must be a kebab-case slug.", nameof(slug));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Value cannot be empty.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(definition))
            throw new ArgumentException("Value cannot be empty.", nameof(definition));

        Slug = slug;
        DisplayName = displayName.Trim();
        Definition = definition.Trim();
        Synonyms = NormalizeSynonyms(synonyms);
    }

    /// <summary>Stable kebab-case role identifier.</summary>
    public string Slug { get; }

    /// <summary>Human-readable role name.</summary>
    public string DisplayName { get; }

    /// <summary>Short role definition used by authoring and validation tools.</summary>
    public string Definition { get; }

    /// <summary>Known English, Czech, legacy, and historically incorrect names for the role.</summary>
    public IReadOnlyList<string> Synonyms { get; }

    private static string[] NormalizeSynonyms(IEnumerable<string>? synonyms)
    {
        if (synonyms is null)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var synonym in synonyms)
        {
            if (string.IsNullOrWhiteSpace(synonym))
                continue;

            var normalized = synonym.Trim();
            if (seen.Add(normalized))
                result.Add(normalized);
        }

        return result.ToArray();
    }

    private static bool IsKebabCaseSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var previousWasDash = false;
        foreach (var ch in value)
        {
            if (ch == '-')
            {
                if (previousWasDash)
                    return false;

                previousWasDash = true;
                continue;
            }

            if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')))
                return false;

            previousWasDash = false;
        }

        return value[0] != '-' && value[^1] != '-';
    }
}
