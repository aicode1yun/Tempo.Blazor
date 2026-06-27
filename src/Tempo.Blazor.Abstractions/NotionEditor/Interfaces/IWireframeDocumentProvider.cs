namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.Components.Wireframe.Models;

public interface IWireframeDocumentProvider
{
    Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId);
    Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document);
    /// <summary>
    /// Creates a new wireframe document. <paramref name="scopeAppId"/> optionally pins the document to a
    /// specific application scope (GUID string); hosts serving more than one app per API key/session use it
    /// to disambiguate the target app for stateless callers such as MCP tools.
    /// </summary>
    Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title, string? scopeAppId = null);
}
