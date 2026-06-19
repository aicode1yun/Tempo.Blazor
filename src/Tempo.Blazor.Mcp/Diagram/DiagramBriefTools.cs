using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP tools for deriving implementation briefs from diagram/draw documents.</summary>
[McpServerToolType]
public static class DiagramBriefTools
{
    [McpServerTool(Name = "diagram_get_implementation_brief")]
    [Description("Return a deterministic implementation brief for a diagram/draw document: pages, layers, nodes, edges and stencil usage. Use after diagram_validate_document succeeds.")]
    public static async Task<string> GetImplementationBrief(
        ITempoDocumentLibraryProvider library,
        IDiagramDocumentProvider documents,
        [Description("Diagram document id (GUID).")] Guid documentId)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Diagram, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }

        var document = await documents.GetDiagramDocumentAsync(documentId);
        if (document is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Diagram {documentId} not found.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            modifiedAt = entry.ModifiedAt,
            brief = DiagramImplementationBrief.Build(document)
        });
    }
}
