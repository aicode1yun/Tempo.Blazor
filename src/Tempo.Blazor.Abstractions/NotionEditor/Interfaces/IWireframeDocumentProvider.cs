namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.Components.Wireframe.Models;

public interface IWireframeDocumentProvider
{
    Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId);
    Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document);
    Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title);
}
