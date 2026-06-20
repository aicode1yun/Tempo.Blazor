using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed document comparison provider used by the demo editor.</summary>
public sealed class DemoDocumentComparisonProvider : IDocumentComparisonProvider
{
    private readonly HttpClient? _http;

    /// <summary>Creates the provider and optionally binds it to the demo API client.</summary>
    public DemoDocumentComparisonProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
    }

    /// <inheritdoc />
    public async Task<DocumentCompareResult> CompareAsync(
        DocumentCompareRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is null)
        {
            throw new InvalidOperationException("Demo API is not available.");
        }

        using var response = await _http.PostAsJsonAsync(
            "api/document-editor/compare",
            request,
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<DocumentCompareResult>(cancellationToken);

        if (response.IsSuccessStatusCode && result is not null)
        {
            return result;
        }

        throw new InvalidOperationException("Document comparison failed.");
    }
}
