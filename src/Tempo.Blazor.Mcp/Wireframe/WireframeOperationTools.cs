using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// MCP write tools for building a wireframe: granular operation batches and whole-document
/// replacement. Both validate the result against the schema registry before persisting, and
/// support optimistic concurrency via expectedModifiedAt.
/// </summary>
[McpServerToolType]
public static class WireframeOperationTools
{
    [McpServerTool(Name = "wireframe_apply_operations")]
    [Description("Apply an ordered batch of edit operations to a wireframe and save it. operationsJson is a JSON array; each item has an 'op' field: setTitle, addPage, updatePage, removePage, setCanvasSize, addElement, updateElement, removeElement, addConnector, updateConnector, removeConnector. Page-targeted operations accept optional pageId; when omitted, the current active page is used. addPage returns the new page id in createdIds but does not make it active, so use that id as pageId for subsequent operations on the new page. The batch is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) to avoid overwriting concurrent edits.")]
    public static async Task<string> ApplyOperationsScoped(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("JSON array of operations.")] string operationsJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null,
        [Description("Optional app id used to resolve local custom type names and scoped app component types during validation.")] string? scopeAppId = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "wireframe_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var current = await documents.GetWireframeDocumentAsync(documentId);
        if (current is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }

        var working = WireframeSerializer.Deserialize(WireframeSerializer.Serialize(current));
        var opResult = WireframeOperationEngine.Apply(working, operationsJson);
        if (!opResult.Success)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "One or more operations failed.", opResult.Errors);
        }

        var validation = WireframeValidationEngine.Validate(working, registry, WireframeComponentScope.FromAppId(scopeAppId));
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The resulting document is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveWireframeDocumentAsync(documentId, working);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);

        return McpToolResults.Success(new
        {
            id = documentId,
            applied = opResult.Applied,
            createdIds = opResult.CreatedIds,
            modifiedAt = saved?.ModifiedAt
        });
    }

    public static Task<string> ApplyOperations(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        Guid documentId,
        string operationsJson,
        DateTime? expectedModifiedAt = null)
        => ApplyOperationsScoped(library, documents, registry, documentId, operationsJson, expectedModifiedAt, scopeAppId: null);

    [McpServerTool(Name = "wireframe_replace_document")]
    [Description("Replace a wireframe's entire content with the provided document JSON and save it. The document is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) for optimistic concurrency.")]
    public static async Task<string> ReplaceDocumentScoped(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("The full replacement wireframe document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null,
        [Description("Optional app id used to resolve local custom type names and scoped app component types during validation.")] string? scopeAppId = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "wireframe_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (!WireframeSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document JSON could not be parsed.");
        }

        var validation = WireframeValidationEngine.Validate(document, registry, WireframeComponentScope.FromAppId(scopeAppId));
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveWireframeDocumentAsync(documentId, document);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);

        return McpToolResults.Success(new { id = documentId, modifiedAt = saved?.ModifiedAt });
    }

    public static Task<string> ReplaceDocument(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        Guid documentId,
        string documentJson,
        DateTime? expectedModifiedAt = null)
        => ReplaceDocumentScoped(library, documents, registry, documentId, documentJson, expectedModifiedAt, scopeAppId: null);

}
