using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed suggestion provider used by the document editor demo.</summary>
public sealed class DemoDocumentSuggestionProvider : InMemoryDocumentSuggestionProvider
{
    private readonly HttpClient? _http;

    /// <summary>Creates the provider and optionally binds it to the demo API client.</summary>
    public DemoDocumentSuggestionProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<DocumentSuggestion>> GetSuggestionsAsync(
        DocumentSuggestionQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var status = query.Status is null ? string.Empty : $"?status={query.Status}";
                var suggestions = await _http.GetFromJsonAsync<List<DocumentSuggestion>>(
                    $"api/document-editor/suggestions/documents/{Uri.EscapeDataString(query.DocumentId)}{status}",
                    cancellationToken);
                if (suggestions is not null)
                {
                    return suggestions;
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.GetSuggestionsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentSuggestion> CreateSuggestionAsync(
        DocumentSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/document-editor/suggestions",
                    suggestion,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<DocumentSuggestion>(cancellationToken);
                    if (created is not null)
                    {
                        return created;
                    }
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.CreateSuggestionAsync(suggestion, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentSuggestion> ReviewSuggestionAsync(
        DocumentSuggestionReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/document-editor/suggestions/review",
                    request,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var reviewed = await response.Content.ReadFromJsonAsync<DocumentSuggestion>(cancellationToken);
                    if (reviewed is not null)
                    {
                        return reviewed;
                    }
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.ReviewSuggestionAsync(request, cancellationToken);
    }
}
