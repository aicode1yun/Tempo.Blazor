using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP validation tools for diagram/draw documents.</summary>
[McpServerToolType]
public static class DiagramValidationTools
{
    [McpServerTool(Name = "diagram_validate_document")]
    [Description("Validate a diagram/draw document JSON payload for page, node, edge and layer integrity. If stencil providers are registered, also validates node stencil ids.")]
    public static string ValidateDocument(
        IEnumerable<IDiagramStencilProvider> stencilProviders,
        [Description("Full diagram document JSON to validate.")] string documentJson)
    {
        if (!DiagramSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The diagram document JSON could not be parsed.");
        }

        var result = DiagramValidationEngine.Validate(document, stencilProviders);
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors
        });
    }
}
