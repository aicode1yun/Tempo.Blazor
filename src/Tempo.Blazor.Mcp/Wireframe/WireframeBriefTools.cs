using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>MCP tool that turns a wireframe into an implementation brief for downstream planning.</summary>
[McpServerToolType]
public static class WireframeBriefTools
{
    [McpServerTool(Name = "wireframe_get_implementation_brief")]
    [Description("Produce a deterministic implementation brief for a wireframe: each page as a section with layout regions (header/sidebar/content/footer) inferred from geometry, the components used (with counts), and navigation flows from connectors. Feed this into a plan/use-case to build the real page.")]
    public static async Task<string> GetImplementationBrief(
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

        var brief = WireframeImplementationBrief.Build(doc);
        return McpToolResults.Success(new
        {
            id = documentId,
            brief.Title,
            brief.Pages,
            brief.ComponentsUsed
        });
    }
}
