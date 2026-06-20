namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.Components.Diagram.Models;

public interface IDiagramDocumentProvider
{
    Task<DiagramDocument?> GetDiagramDocumentAsync(Guid documentId);
    Task<DiagramDocument> SaveDiagramDocumentAsync(Guid documentId, DiagramDocument document);
    Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title);
}
