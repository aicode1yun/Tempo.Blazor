using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Demo.Shared;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// <see cref="IDiagramDocumentProvider"/> backed by the Demo.Api document-library.
/// </summary>
public sealed class ApiDiagramDocumentProvider : IDiagramDocumentProvider
{
    private const string Kind = "diagram";
    private readonly HttpClient _http;

    public ApiDiagramDocumentProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title)
    {
        var doc = new DiagramDocument { Title = string.IsNullOrWhiteSpace(title) ? "Untitled diagram" : title };

        var resp = await _http.PostAsJsonAsync(
            $"/api/document-library/{Kind}/documents",
            new DocumentLibraryCreateRequest
            {
                Name = doc.Title,
                FolderPath = "/",
                PayloadJson = DiagramSerializer.Serialize(doc)
            });
        resp.EnsureSuccessStatusCode();
        var meta = (await resp.Content.ReadFromJsonAsync<DocumentLibraryMetadataDto>())!;
        return (meta.Id, doc);
    }

    public async Task<DiagramDocument?> GetDiagramDocumentAsync(Guid documentId)
    {
        var resp = await _http.GetAsync($"/api/document-library/{Kind}/documents/{documentId}/payload");
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        resp.EnsureSuccessStatusCode();
        return DiagramSerializer.Deserialize(await resp.Content.ReadAsStringAsync());
    }

    public async Task<DiagramDocument> SaveDiagramDocumentAsync(Guid documentId, DiagramDocument document)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/document-library/{Kind}/documents/{documentId}/payload",
            new DocumentLibrarySaveRequest
            {
                PayloadJson = DiagramSerializer.Serialize(document),
                Name = document.Title
            });
        resp.EnsureSuccessStatusCode();
        return document;
    }
}
