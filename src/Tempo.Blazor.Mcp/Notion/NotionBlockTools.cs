using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Notion;

/// <summary>MCP block tools for NotionEditor.</summary>
[McpServerToolType]
public static class NotionBlockTools
{
    [McpServerTool(Name = "notion_list_blocks")]
    [Description("List NotionEditor blocks for a page or child blocks under parentBlockId.")]
    public static async Task<string> ListBlocks(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        [Description("Page id for top-level blocks.")] string pageId,
        [Description("Optional parent block id for child blocks.")] string? parentBlockId = null)
    {
        if (await NotionPageTools.LoadPageForWrite(pages, pageId, expectedLastEditedAt: null, "notion_get_page") is { } failure)
        {
            return failure;
        }

        var items = string.IsNullOrWhiteSpace(parentBlockId)
            ? await blocks.GetBlocksAsync(pageId)
            : await blocks.GetChildBlocksAsync(parentBlockId);

        var list = items.OrderBy(b => b.Order).ToList();
        return McpToolResults.Success(new { pageId, parentBlockId, totalCount = list.Count, items = list });
    }

    [McpServerTool(Name = "notion_get_block_tree")]
    [Description("Get all blocks for a NotionEditor page, including nested child blocks.")]
    public static async Task<string> GetBlockTree(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        [Description("Page id.")] string pageId)
    {
        if (await NotionPageTools.LoadPageForWrite(pages, pageId, expectedLastEditedAt: null, "notion_get_page") is { } failure)
        {
            return failure;
        }

        var all = await NotionPageTools.LoadBlocks(blocks, pageId, recursive: true);
        return McpToolResults.Success(new
        {
            pageId,
            totalCount = all.Count,
            blocks = all.OrderBy(b => b.ParentBlockId).ThenBy(b => b.Order).ToList()
        });
    }

}
