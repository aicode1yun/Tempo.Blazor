using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Mcp.Notion;

/// <summary>MCP discovery tools for canonical Notion authoring.</summary>
[McpServerToolType]
public static class NotionSchemaAndValidationTools
{
    [McpServerTool(Name = "notion_list_block_types")]
    [Description("List canonical Notion block types with complete content field metadata available through notion_get_block_schema.")]
    public static string ListBlockTypes(
        [Description("Optional free-text filter across block type, content type and description.")] string? search = null)
    {
        var items = NotionAuthoringCatalog.ListBlockTypes().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(item =>
                item.ToJsonString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = items.ToList();
        return McpToolResults.Success(new { totalCount = list.Count, items = list });
    }

    [McpServerTool(Name = "notion_get_block_schema")]
    [Description("Return the complete canonical content schema for one Notion BlockType, including required/optional/null/default semantics, enums, nested fields, limits, styles and an example.")]
    public static string GetBlockSchema(
        [Description("BlockType name, e.g. Paragraph, Code, Table or TableRow.")] string type)
    {
        if (!Enum.TryParse<BlockType>(type, ignoreCase: true, out var parsed))
        {
            return McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Block type '{type}' not found.");
        }

        var schema = NotionAuthoringCatalog.TryGetBlockSchema(parsed);
        return schema is null
            ? McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Block type '{type}' not found.")
            : McpToolResults.Success(new { blockType = schema });
    }

    [McpServerTool(Name = "notion_get_operation_catalog")]
    [Description("Return strict atomic operation schemas as JSON data. Each field includes required/optional/null/default semantics, enums and an executable example.")]
    public static string GetOperationCatalog(
        [Description("Optional exact operation name, e.g. createTable, patchBlockContent or moveBlock. Omit for all operations.")] string? operation = null)
    {
        var operations = NotionAuthoringCatalog.GetOperationCatalog(operation);
        if (!string.IsNullOrWhiteSpace(operation) && operations.Count == 0)
        {
            return McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Operation '{operation}' not found.");
        }

        return McpToolResults.Success(new
        {
            totalCount = operations.Count,
            operations
        });
    }

    [McpServerTool(Name = "notion_get_authoring_guide")]
    [Description("Return the canonical agent authoring workflow as JSON data: recursive children, createTable, patch, move, concurrency and idempotent retry.")]
    public static string GetAuthoringGuide(
        [Description("Optional topic: readBeforeWrite, atomicWrite, recursiveChildren, createTable, patch, move or discovery. Omit for the complete guide.")] string? topic = null)
        => McpToolResults.Success(new
        {
            guide = NotionAuthoringCatalog.GetAuthoringGuide(topic)
        });
}
