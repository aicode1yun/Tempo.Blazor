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
    [Description("Apply an ordered batch of edit operations to a wireframe and save it. operationsJson is a JSON array; each item has an 'op' field: setTitle, addPage, updatePage, removePage, setCanvasSize, addElement, updateElement, removeElement, addConnector, updateConnector, removeConnector. The batch is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) to avoid overwriting concurrent edits.")]
    public static async Task<string> ApplyOperations(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("JSON array of operations.")] string operationsJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (ConflictMessage(expectedModifiedAt, entry.ModifiedAt) is { } conflict)
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

        var validation = WireframeValidationEngine.Validate(working, registry);
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

    [McpServerTool(Name = "wireframe_replace_document")]
    [Description("Replace a wireframe's entire content with the provided document JSON and save it. The document is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) for optimistic concurrency.")]
    public static async Task<string> ReplaceDocument(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("The full replacement wireframe document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (ConflictMessage(expectedModifiedAt, entry.ModifiedAt) is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (!WireframeSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document JSON could not be parsed.");
        }

        var validation = WireframeValidationEngine.Validate(document, registry);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveWireframeDocumentAsync(documentId, document);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);

        return McpToolResults.Success(new { id = documentId, modifiedAt = saved?.ModifiedAt });
    }

    private static string? ConflictMessage(DateTime? expected, DateTime current)
    {
        if (expected is null)
        {
            return null;
        }

        return Math.Abs((current - expected.Value).TotalMilliseconds) > 1
            ? $"The document was modified since you read it (current modifiedAt {current:O}). Re-read with wireframe_get_document and retry."
            : null;
    }
}
