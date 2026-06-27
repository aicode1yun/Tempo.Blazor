using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public class MockNotionWireframeDocumentProvider : IWireframeDocumentProvider
{
    private readonly Dictionary<Guid, WireframeDocument> _store = new();

    public Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title, string? scopeAppId = null)
    {
        _ = scopeAppId;
        var id  = Guid.NewGuid();
        var doc = new WireframeDocument { Title = title };
        doc.EnsureActivePage();
        _store[id] = doc;
        return Task.FromResult((id, doc));
    }

    public Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId)
    {
        _store.TryGetValue(documentId, out var doc);
        return Task.FromResult(doc);
    }

    public Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document)
    {
        _store[documentId] = document;
        return Task.FromResult(document);
    }
}
