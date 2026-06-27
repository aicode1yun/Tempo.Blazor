namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.Components.Diagram.Models;

public interface IDiagramDocumentProvider
{
    Task<DiagramDocument?> GetDiagramDocumentAsync(Guid documentId);
    Task<DiagramDocument> SaveDiagramDocumentAsync(Guid documentId, DiagramDocument document);
    Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title);

    /// <summary>
    /// Creates a diagram document scoped to a specific application. <paramref name="scopeAppId"/> (GUID
    /// string) lets multi-app hosts disambiguate the target app for stateless callers such as MCP tools.
    /// Default implementation ignores the scope and delegates to <see cref="CreateDiagramDocumentAsync(string)"/>;
    /// multi-app providers override it.
    /// </summary>
    Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title, string? scopeAppId)
        => CreateDiagramDocumentAsync(title);
}
