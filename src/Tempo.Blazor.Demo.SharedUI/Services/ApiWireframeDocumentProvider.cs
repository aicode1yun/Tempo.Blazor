using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Demo.Shared;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// <see cref="IWireframeDocumentProvider"/> backed by the Demo.Api document-library, so wireframe
/// payloads live in the shared store (visible to the open dialog, MCP tooling and live refresh).
/// </summary>
public sealed class ApiWireframeDocumentProvider : IWireframeDocumentProvider
{
    private const string Kind = "wireframe";
    private readonly HttpClient _http;

    public ApiWireframeDocumentProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title)
    {
        var doc = new WireframeDocument { Title = string.IsNullOrWhiteSpace(title) ? "Untitled wireframe" : title };
        doc.EnsureActivePage();

        var resp = await _http.PostAsJsonAsync(
            $"/api/document-library/{Kind}/documents",
            new DocumentLibraryCreateRequest
            {
                Name = doc.Title,
                FolderPath = "/",
                PayloadJson = WireframeSerializer.Serialize(doc)
            });
        resp.EnsureSuccessStatusCode();
        var meta = (await resp.Content.ReadFromJsonAsync<DocumentLibraryMetadataDto>())!;
        return (meta.Id, doc);
    }

    public async Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId)
    {
        var resp = await _http.GetAsync($"/api/document-library/{Kind}/documents/{documentId}/payload");
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return WireframeSerializer.Deserialize(json);
    }

    public async Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/document-library/{Kind}/documents/{documentId}/payload",
            new DocumentLibrarySaveRequest
            {
                PayloadJson = WireframeSerializer.Serialize(document),
                Name = document.Title
            });
        resp.EnsureSuccessStatusCode();
        return document;
    }
}
