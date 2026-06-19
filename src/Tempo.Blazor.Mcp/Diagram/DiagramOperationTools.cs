using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP write tools for diagram/draw documents.</summary>
[McpServerToolType]
public static class DiagramOperationTools
{
    [McpServerTool(Name = "diagram_apply_operations")]
    [Description("Apply an ordered batch of edit operations to a diagram/draw document and save it. operationsJson is a JSON array; each item has an 'op' field: setTitle, addPage, updatePage, removePage, setActivePage, setCanvasSize, addNode, updateNode, removeNode, addEdge, updateEdge, removeEdge, addLayer, updateLayer, removeLayer, reorderLayers, moveItemsToLayer. Pass expectedModifiedAt from diagram_get_document to avoid overwriting concurrent edits.")]
    public static async Task<string> ApplyOperations(
        ITempoDocumentLibraryProvider library,
        IDiagramDocumentProvider documents,
        IEnumerable<IDiagramStencilProvider> stencilProviders,
        [Description("Diagram document id (GUID).")] Guid documentId,
        [Description("JSON array of operations.")] string operationsJson,
        [Description("Optional optimistic-concurrency token from diagram_get_document.")] DateTime? expectedModifiedAt = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "diagram_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var current = await documents.GetDiagramDocumentAsync(documentId);
        if (current is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }

        var working = McpJsonHelpers.Clone(current, DiagramJsonOptions.Default);
        var opResult = DiagramOperationEngine.Apply(working, operationsJson);
        if (!opResult.Success)
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, "One or more operations failed.", opResult.Errors);
        }

        var validation = DiagramValidationEngine.Validate(working, stencilProviders);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The resulting diagram is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveDiagramDocumentAsync(documentId, working);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);

        return McpToolResults.Success(new
        {
            id = documentId,
            applied = opResult.Applied,
            createdIds = opResult.CreatedIds,
            modifiedAt = saved?.ModifiedAt
        });
    }

    [McpServerTool(Name = "diagram_replace_document")]
    [Description("Replace a diagram/draw document's entire content with the provided document JSON and save it. The document is validated before saving. Pass expectedModifiedAt from diagram_get_document for optimistic concurrency.")]
    public static async Task<string> ReplaceDocument(
        ITempoDocumentLibraryProvider library,
        IDiagramDocumentProvider documents,
        IEnumerable<IDiagramStencilProvider> stencilProviders,
        [Description("Diagram document id (GUID).")] Guid documentId,
        [Description("The full replacement diagram document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from diagram_get_document.")] DateTime? expectedModifiedAt = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "diagram_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (!DiagramSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The diagram document JSON could not be parsed.");
        }

        var validation = DiagramValidationEngine.Validate(document, stencilProviders);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The diagram document is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveDiagramDocumentAsync(documentId, document);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);

        return McpToolResults.Success(new { id = documentId, modifiedAt = saved?.ModifiedAt });
    }
}
