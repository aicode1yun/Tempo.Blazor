using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public class MockNotionDiagramDocumentProvider : IDiagramDocumentProvider
{
    private readonly Dictionary<Guid, DiagramDocument> _store = new();

    public Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title)
    {
        var id  = Guid.NewGuid();
        var doc = new DiagramDocument { Id = id.ToString(), Title = title };
        _store[id] = doc;
        return Task.FromResult((id, doc));
    }

    public Task<DiagramDocument?> GetDiagramDocumentAsync(Guid documentId)
    {
        _store.TryGetValue(documentId, out var doc);
        return Task.FromResult(doc);
    }

    public Task<DiagramDocument> SaveDiagramDocumentAsync(Guid documentId, DiagramDocument document)
    {
        _store[documentId] = document;
        return Task.FromResult(document);
    }
}
