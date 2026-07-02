using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>MCP tool for validating a wireframe document against the component schema registry.</summary>
[McpServerToolType]
public static class WireframeValidationTools
{
    [McpServerTool(Name = "wireframe_validate_document")]
    [Description("Validate a wireframe document JSON against the component schema registry. Returns success with valid=true/false, validationErrors for structural problems (unknown types, bad sizes, dangling connectors, duplicate ids), and warnings for prop/enum issues such as unknown-prop, enum-normalized, enum-out-of-range and type-mismatch plus advisory document warnings default-size, off-canvas, overlap, text-overflow and empty-required-content. Call this before saving a generated design.")]
    public static string ValidateDocumentScoped(
        WireframeSchemaRegistry registry,
        [Description("The full wireframe document JSON to validate.")] string documentJson,
        [Description("Optional app id used to resolve local custom type names and scoped app component types.")] string? scopeAppId = null)
    {
        if (!WireframeSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document JSON could not be parsed.");
        }

        var result = WireframeValidationEngine.Validate(document, registry, WireframeComponentScope.FromAppId(scopeAppId));
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors,
            warnings = result.Warnings
        });
    }

    public static string ValidateDocument(WireframeSchemaRegistry registry, string documentJson)
        => ValidateDocumentScoped(registry, documentJson, scopeAppId: null);
}
