using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Materializes asynchronous <see cref="ITempoProofingProvider"/> results into the word-list based
/// <see cref="DocumentProofingOptions"/> consumed by the canvas proofing runtime. Host-supplied
/// base options (custom flagged words and suggestions) are preserved and merged with the provider
/// findings.
/// </summary>
public static class DocumentProofingService
{
    /// <summary>Builds effective proofing options from a provider result merged over host base options.</summary>
    public static DocumentProofingOptions BuildOptions(
        DocumentProofingCheckResult? result,
        DocumentProofingOptions? baseOptions = null)
    {
        var flagged = new List<string>();
        var flaggedLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in baseOptions?.FlaggedWords ?? [])
        {
            if (!string.IsNullOrWhiteSpace(word) && flaggedLookup.Add(word))
            {
                flagged.Add(word);
            }
        }

        foreach (var pair in baseOptions?.Suggestions ?? new Dictionary<string, IReadOnlyList<string>>())
        {
            suggestions[pair.Key] = pair.Value;
        }

        foreach (var issue in result?.Issues ?? [])
        {
            var word = issue.Word?.Trim();
            if (string.IsNullOrEmpty(word))
            {
                continue;
            }

            if (flaggedLookup.Add(word))
            {
                flagged.Add(word);
            }

            if (issue.Suggestions.Count == 0)
            {
                continue;
            }

            var merged = suggestions.TryGetValue(word, out var existing)
                ? existing.ToList()
                : [];
            foreach (var suggestion in issue.Suggestions)
            {
                if (!string.IsNullOrWhiteSpace(suggestion)
                    && !merged.Contains(suggestion, StringComparer.Ordinal))
                {
                    merged.Add(suggestion);
                }
            }

            if (merged.Count > 0)
            {
                suggestions[word] = merged;
            }
        }

        return new DocumentProofingOptions
        {
            Enabled = baseOptions?.Enabled ?? true,
            DefaultLanguage = baseOptions?.DefaultLanguage,
            FlaggedWords = flagged,
            Suggestions = suggestions
        };
    }
}
