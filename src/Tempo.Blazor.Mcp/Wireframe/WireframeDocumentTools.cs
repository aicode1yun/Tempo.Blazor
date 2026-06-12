using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// MCP tools for listing, reading and creating stored wireframe documents. Persistence (and
/// therefore live-change notification) is delegated to the host-supplied providers.
/// </summary>
[McpServerToolType]
public static class WireframeDocumentTools
{
    [McpServerTool(Name = "wireframe_list_documents")]
    [Description("List stored wireframe documents (id, name, folder, last-modified). Filter by folderPath or a free-text search. Use the id with wireframe_get_document.")]
    public static async Task<string> ListDocuments(
        ITempoDocumentLibraryProvider library,
        [Description("Optional folder to list (e.g. '/Designs'). Omit for the root.")] string? folderPath = null,
        [Description("Optional free-text search across document names.")] string? search = null,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of documents to return (default 50).")] int take = 50)
    {
        var page = await library.BrowseAsync(new DocumentLibraryQuery
        {
            Kind = TempoDocumentKind.Wireframe,
            FolderPath = folderPath,
            Search = search,
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, 500)
        });

        return McpToolResults.Success(new
        {
            totalCount = page.TotalCount,
            items = page.Items.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                folderPath = i.FolderPath,
                modifiedAt = i.ModifiedAt
            }).ToList()
        });
    }

    [McpServerTool(Name = "wireframe_get_document")]
    [Description("Get a wireframe document by id: its current modifiedAt (the optimistic-concurrency token for writes) and the full document JSON. Returns not_found if the document does not exist.")]
    public static async Task<string> GetDocument(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        [Description("Wireframe document id (GUID).")] Guid documentId)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }

        var doc = await documents.GetWireframeDocumentAsync(documentId);
        if (doc is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            modifiedAt = entry.ModifiedAt,
            document = JsonNode.Parse(WireframeSerializer.Serialize(doc))
        });
    }

    [McpServerTool(Name = "wireframe_create_document")]
    [Description("Create a new empty wireframe document with the given title and return its id and modifiedAt. Then use wireframe_apply_operations or wireframe_replace_document to build it.")]
    public static async Task<string> CreateDocument(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        [Description("Title for the new wireframe.")] string title)
    {
        var (id, _) = await documents.CreateWireframeDocumentAsync(title);
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, id);

        return McpToolResults.Success(new
        {
            id,
            modifiedAt = entry?.ModifiedAt
        });
    }
}
