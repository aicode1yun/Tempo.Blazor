using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>MCP tool for validating a wireframe document against the component schema registry.</summary>
[McpServerToolType]
public static class WireframeValidationTools
{
    [McpServerTool(Name = "wireframe_validate_document")]
    [Description("Validate a wireframe document JSON against the component schema registry. Returns success with valid=true/false and a list of precise validationErrors (unknown types/props, invalid enum values, bad sizes, dangling connectors, duplicate ids). Call this before saving a generated design.")]
    public static string ValidateDocument(
        WireframeSchemaRegistry registry,
        [Description("The full wireframe document JSON to validate.")] string documentJson)
    {
        if (!WireframeSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document JSON could not be parsed.");
        }

        var result = WireframeValidationEngine.Validate(document, registry);
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors
        });
    }
}
