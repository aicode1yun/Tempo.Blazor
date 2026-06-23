using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>MCP validation and schema documentation tools for report definitions.</summary>
[McpServerToolType]
public static class ReportValidationTools
{
    [McpServerTool(Name = "validate_report")]
    [Description("Validate report definition JSON and return valid=true/false plus validationErrors. Set includeSchema=true to include the AI-friendly report definition schema and element-type documentation.")]
    public static string ValidateReport(
        [Description("Full report definition JSON.")] string definitionJson,
        [Description("Include PromptHelper-style schema documentation for report JSON authoring.")] bool includeSchema = false)
    {
        var result = ReportValidationEngine.ValidateJson(definitionJson);
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors,
            schema = includeSchema ? ReportDefinitionPromptSchema.Build() : null,
        });
    }
}
