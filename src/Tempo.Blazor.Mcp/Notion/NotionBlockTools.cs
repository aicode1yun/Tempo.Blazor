using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

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

    [McpServerTool(Name = "notion_apply_block_operations")]
    [Description("Apply an ordered JSON array of Notion page/block operations: createBlock, createBlocks, updateBlockContent, deleteBlock, reorderBlocks, moveBlock, moveBlockToPage, duplicateBlock, convertBlockType, setPageLabels, toggleFavorite, createDiagramBlock, createWireframeBlock, createSpreadsheetBlock.")]
    public static async Task<string> ApplyBlockOperations(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        [Description("JSON array of operations.")] string operationsJson,
        [Description("Optional page id whose LastEditedAt should be checked before applying the operation batch.")] string? expectedPageId = null,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (expectedLastEditedAt is not null)
        {
            if (string.IsNullOrWhiteSpace(expectedPageId))
            {
                return McpToolResults.Failure(
                    McpToolResults.ValidationFailed,
                    "expectedPageId is required when expectedLastEditedAt is supplied.");
            }

            if (await NotionPageTools.LoadPageForWrite(pages, expectedPageId, expectedLastEditedAt, "notion_get_page") is { } failure)
            {
                return failure;
            }
        }

        var result = await NotionOperationEngine.ApplyAsync(pages, blocks, operationsJson);
        if (!result.Success)
        {
            return McpToolResults.Failure(result.ErrorCode, "One or more Notion operations failed.", result.Errors);
        }

        return McpToolResults.Success(new
        {
            applied = result.Applied,
            createdIds = result.CreatedIds
        });
    }

    [McpServerTool(Name = "notion_replace_blocks")]
    [Description("Replace all top-level blocks on a page. Existing top-level blocks are deleted, then blocksJson (a JSON array of PageBlock objects) is created in order.")]
    public static async Task<string> ReplaceBlocks(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        [Description("Page id.")] string pageId,
        [Description("JSON array of PageBlock objects.")] string blocksJson,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (await NotionPageTools.LoadPageForWrite(pages, pageId, expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        List<PageBlock>? replacement;
        try
        {
            replacement = JsonSerializer.Deserialize<List<PageBlock>>(blocksJson, NotionMcpJson.Options);
        }
        catch (JsonException ex)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The blocks JSON could not be parsed: {ex.Message}");
        }

        replacement ??= [];
        for (var i = 0; i < replacement.Count; i++)
        {
            replacement[i].PageId = Guid.Parse(pageId);
            replacement[i].Order = i;
            if (replacement[i].Id == Guid.Empty)
            {
                replacement[i].Id = Guid.NewGuid();
            }
        }

        var validation = NotionValidationEngine.Validate(null, replacement);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The replacement blocks are invalid; nothing was saved.", validation.Errors);
        }

        var existing = (await blocks.GetBlocksAsync(pageId)).ToList();
        foreach (var block in existing)
        {
            await NotionOperationEngine.DeleteBlockTreeAsync(blocks, block.Id.ToString());
        }

        var created = await blocks.CreateBlocksAsync(pageId, replacement, afterBlockId: null);
        return McpToolResults.Success(new
        {
            pageId,
            replaced = existing.Count,
            createdIds = created.Select(b => b.Id).ToList()
        });
    }
}
