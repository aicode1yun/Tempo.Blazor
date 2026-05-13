using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed PDF export provider used by the document editor demo.</summary>
public sealed class DemoDocumentPdfExportProvider : IDocumentPdfExportProvider
{
    private readonly HttpClient? _http;

    /// <summary>Creates the provider and optionally binds it to the demo API client.</summary>
    public DemoDocumentPdfExportProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
    }

    /// <inheritdoc />
    public async Task<DocumentPdfExportResult> ExportPdfAsync(
        DocumentPdfExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is null)
        {
            throw new InvalidOperationException("Demo API is not available.");
        }

        var documentId = Uri.EscapeDataString(request.DocumentId);
        using var response = await _http.PostAsJsonAsync(
            $"api/document-editor/{documentId}/export/pdf",
            request,
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<DocumentPdfExportResult>(
            cancellationToken);

        if (response.IsSuccessStatusCode && result is not null)
        {
            return result;
        }

        throw new InvalidOperationException("PDF export failed.");
    }
}
