using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP tools for listing, reading and creating stored diagram/draw documents.</summary>
[McpServerToolType]
public static class DiagramDocumentTools
{
    [McpServerTool(Name = "diagram_list_documents")]
    [Description("List stored diagram/draw documents (id, name, folder, last-modified). Filter by folderPath or search. Use the id with diagram_get_document.")]
    public static async Task<string> ListDocuments(
        ITempoDocumentLibraryProvider library,
        [Description("Optional folder to list (e.g. '/Diagrams'). Omit for the root.")] string? folderPath = null,
        [Description("Optional free-text search across document names.")] string? search = null,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of documents to return (default 50).")] int take = 50,
        [Description("Optional app id (GUID) scoping the listing; required when the API key grants access to more than one app.")] string? scopeAppId = null)
    {
        var page = await library.BrowseAsync(new DocumentLibraryQuery
        {
            Kind = TempoDocumentKind.Diagram,
            FolderPath = folderPath,
            Search = search,
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, 500),
            ScopeAppId = scopeAppId
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

    [McpServerTool(Name = "diagram_get_document")]
    [Description("Get a diagram/draw document by id: current modifiedAt (optimistic-concurrency token) and full document JSON. Returns not_found if missing.")]
    public static async Task<string> GetDocument(
        ITempoDocumentLibraryProvider library,
        IDiagramDocumentProvider documents,
        [Description("Diagram document id (GUID).")] Guid documentId)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }

        var doc = await documents.GetDiagramDocumentAsync(documentId);
        if (doc is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            modifiedAt = entry.ModifiedAt,
            document = JsonSerializer.SerializeToNode(doc, DiagramJsonOptions.Default)
        });
    }

    [McpServerTool(Name = "diagram_create_document")]
    [Description("Create a new empty diagram/draw document with the given title and return its id and modifiedAt. Then use diagram_apply_operations or diagram_replace_document to build it. Pass scopeAppId (app GUID) when your API key grants access to more than one app.")]
    public static async Task<string> CreateDocument(
        ITempoDocumentLibraryProvider library,
        IDiagramDocumentProvider documents,
        [Description("Title for the new diagram/draw document.")] string title,
        [Description("Optional app id (GUID) scoping the new diagram; required when the API key grants access to more than one app.")] string? scopeAppId = null)
    {
        var (id, document) = await documents.CreateDiagramDocumentAsync(title, scopeAppId);
        var entry = await library.GetEntryAsync(TempoDocumentKind.Diagram, id);

        return McpToolResults.Success(new
        {
            id,
            modifiedAt = entry?.ModifiedAt ?? document.ModifiedAt,
            document = JsonSerializer.SerializeToNode(document, DiagramJsonOptions.Default)
        });
    }
}
