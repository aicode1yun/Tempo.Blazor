using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Notion;

/// <summary>MCP schema and validation tools for NotionEditor.</summary>
[McpServerToolType]
public static class NotionSchemaAndValidationTools
{
    [McpServerTool(Name = "notion_validate_page")]
    [Description("Validate a Notion page/block payload. pageJson is optional; blocksJson is a JSON array of PageBlock objects.")]
    public static string ValidatePage(
        [Description("Optional NotionPage JSON object.")] string? pageJson = null,
        [Description("JSON array of PageBlock objects.")] string? blocksJson = null)
    {
        INotionPage? page = null;
        if (!string.IsNullOrWhiteSpace(pageJson))
        {
            try
            {
                page = JsonSerializer.Deserialize<NotionPage>(pageJson, NotionMcpJson.Options);
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The page JSON could not be parsed: {ex.Message}");
            }
        }

        IReadOnlyList<IPageBlock> blocks = [];
        if (!string.IsNullOrWhiteSpace(blocksJson))
        {
            try
            {
                blocks = JsonSerializer.Deserialize<List<PageBlock>>(blocksJson, NotionMcpJson.Options) ?? [];
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The blocks JSON could not be parsed: {ex.Message}");
            }
        }

        var result = NotionValidationEngine.Validate(page, blocks);
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors
        });
    }

    [McpServerTool(Name = "notion_list_block_types")]
    [Description("List NotionEditor block types and the required polymorphic content discriminator for each type.")]
    public static string ListBlockTypes(
        [Description("Optional free-text filter across type and content type.")] string? search = null)
    {
        var items = NotionBlockCatalog.All.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(i =>
                i.Type.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
                || i.ContentType.Contains(search, StringComparison.OrdinalIgnoreCase)
                || i.ContentDiscriminator.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = items.ToList();
        return McpToolResults.Success(new { totalCount = list.Count, items = list });
    }

    [McpServerTool(Name = "notion_get_block_schema")]
    [Description("Return the schema descriptor for one NotionEditor BlockType.")]
    public static string GetBlockSchema(
        [Description("BlockType name, e.g. Paragraph, Heading1, Diagram.")] string type)
    {
        if (!Enum.TryParse<BlockType>(type, ignoreCase: true, out var parsed))
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Block type '{type}' not found.");
        }

        var schema = NotionBlockCatalog.Get(parsed);
        return schema is null
            ? McpToolResults.Failure(McpToolResults.NotFound, $"Block type '{type}' not found.")
            : McpToolResults.Success(new { blockType = schema });
    }
}
