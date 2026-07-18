using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Proofing.LanguageTool;

/// <summary>
/// Reference <see cref="ITempoProofingProvider"/> speaking the LanguageTool v2 HTTP protocol
/// (<c>POST {base}/v2/check</c> with form-encoded <c>text</c> + <c>language</c>). Works against a
/// self-hosted LanguageTool container (see <c>docs/proofing-languagetool.md</c>) or any
/// protocol-compatible endpoint. Findings whose word is in
/// <see cref="LanguageToolProofingOptions.CustomDictionary"/> are suppressed client-side.
/// </summary>
public sealed class LanguageToolProofingProvider : ITempoProofingProvider
{
    private static readonly Uri DefaultBaseAddress = new("http://localhost:8010");

    private readonly HttpClient _httpClient;
    private readonly LanguageToolProofingOptions _options;

    /// <summary>Creates the provider over an HTTP client and optional configuration.</summary>
    public LanguageToolProofingProvider(HttpClient httpClient, LanguageToolProofingOptions? options = null)
    {
        _httpClient = httpClient;
        _options = options ?? new LanguageToolProofingOptions();
    }

    /// <summary>Adds a word to the client-side custom dictionary so it is no longer reported.</summary>
    public void AddToDictionary(string word)
    {
        if (!string.IsNullOrWhiteSpace(word))
        {
            _options.CustomDictionary.Add(word.Trim());
        }
    }

    /// <inheritdoc />
    public async Task<DocumentProofingCheckResult> CheckAsync(
        DocumentProofingCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = request.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return DocumentProofingCheckResult.Empty;
        }

        if (text.Length > _options.MaxTextLength)
        {
            text = text[.._options.MaxTextLength];
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("text", text),
            new("language", string.IsNullOrWhiteSpace(request.Language) ? _options.Language : request.Language)
        };
        if (!string.IsNullOrWhiteSpace(_options.MotherTongue))
        {
            form.Add(new KeyValuePair<string, string>("motherTongue", _options.MotherTongue));
        }

        if (_options.DisabledRules.Count > 0)
        {
            form.Add(new KeyValuePair<string, string>("disabledRules", string.Join(",", _options.DisabledRules)));
        }

        if (_options.DisabledCategories.Count > 0)
        {
            form.Add(new KeyValuePair<string, string>("disabledCategories", string.Join(",", _options.DisabledCategories)));
        }

        using var response = await _httpClient.PostAsync(
            BuildCheckUri(),
            new FormUrlEncodedContent(form),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return new DocumentProofingCheckResult { Issues = ParseIssues(payload, text) };
    }

    private Uri BuildCheckUri()
    {
        var baseAddress = _options.BaseAddress ?? _httpClient.BaseAddress ?? DefaultBaseAddress;
        return new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/v2/check");
    }

    private IReadOnlyList<DocumentProofingIssue> ParseIssues(string payload, string text)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("matches", out var matches)
            || matches.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var issues = new List<DocumentProofingIssue>();
        foreach (var match in matches.EnumerateArray())
        {
            var offset = ReadInt(match, "offset");
            var length = ReadInt(match, "length");
            if (offset < 0 || length <= 0 || offset + length > text.Length)
            {
                continue;
            }

            var word = text.Substring(offset, length).Trim();
            if (word.Length == 0 || _options.CustomDictionary.Contains(word))
            {
                continue;
            }

            issues.Add(new DocumentProofingIssue
            {
                Word = word,
                Offset = offset,
                Length = length,
                Message = ReadString(match, "message"),
                RuleId = ReadNestedString(match, "rule", "id"),
                CategoryId = ReadNestedString(match, "rule", "category", "id"),
                Suggestions = ReadSuggestions(match)
            });
        }

        return issues;
    }

    private IReadOnlyList<string> ReadSuggestions(JsonElement match)
    {
        if (!match.TryGetProperty("replacements", out var replacements)
            || replacements.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var suggestions = new List<string>();
        foreach (var replacement in replacements.EnumerateArray())
        {
            var value = ReadString(replacement, "value")?.Trim();
            if (!string.IsNullOrEmpty(value) && !suggestions.Contains(value, StringComparer.Ordinal))
            {
                suggestions.Add(value);
                if (suggestions.Count >= _options.MaxSuggestionsPerIssue)
                {
                    break;
                }
            }
        }

        return suggestions;
    }

    private static int ReadInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : -1;

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path[..^1])
        {
            if (!current.TryGetProperty(segment, out current) || current.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
        }

        return ReadString(current, path[^1]);
    }
}
