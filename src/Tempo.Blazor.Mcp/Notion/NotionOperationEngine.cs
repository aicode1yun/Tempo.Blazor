using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

public sealed record NotionOperationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    int Applied,
    IReadOnlyList<string> CreatedIds,
    string ErrorCode = McpToolResults.InvalidOperation);

/// <summary>Applies ordered Notion page/block operations through host providers.</summary>
public static class NotionOperationEngine
{
    public static async Task<NotionOperationResult> ApplyAsync(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        string operationsJson)
    {
        if (!McpJsonHelpers.TryParseOperationArray(operationsJson, out var ops, out var errors) || ops is null)
        {
            return new NotionOperationResult(false, errors, 0, []);
        }

        var created = new List<string>();
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is not JsonObject op)
            {
                return Fail(i, "operation must be an object.", created);
            }

            var name = op["op"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Fail(i, "missing 'op' discriminator.", created);
            }

            string? error;
            try
            {
                error = name switch
                {
                    "createBlock" => await CreateBlock(pages, blocks, op, created),
                    "createBlocks" => await CreateBlocks(pages, blocks, op, created),
                    "updateBlockContent" => await UpdateBlock(blocks, op),
                    "deleteBlock" => await DeleteBlock(blocks, op),
                    "reorderBlocks" => await ReorderBlocks(pages, blocks, op),
                    "moveBlock" => await MoveBlock(pages, blocks, op),
                    "moveBlockToPage" => await MoveBlockToPage(pages, blocks, op),
                    "duplicateBlock" => await DuplicateBlock(blocks, op, created),
                    "convertBlockType" => await ConvertBlockType(blocks, op, created),
                    "setPageLabels" => await SetPageLabels(pages, op),
                    "toggleFavorite" => await ToggleFavorite(pages, op),
                    "createDiagramBlock" => await CreateEmbeddedBlock(pages, blocks, op, BlockType.Diagram, created),
                    "createWireframeBlock" => await CreateEmbeddedBlock(pages, blocks, op, BlockType.Wireframe, created),
                    "createSpreadsheetBlock" => await CreateEmbeddedBlock(pages, blocks, op, BlockType.Spreadsheet, created),
                    _ => $"unknown op '{name}'."
                };
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException or FormatException)
            {
                error = ex.Message;
            }

            if (error is not null)
            {
                return Fail(i, error, created);
            }
        }

        return new NotionOperationResult(true, [], ops.Count, created);

        static NotionOperationResult Fail(int index, string message, List<string> created)
        {
            var errorCode = message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? McpToolResults.NotFound
                : McpToolResults.InvalidOperation;
            return new(false, [$"operations[{index}]: {message}"], 0, created, errorCode);
        }
    }

    private static async Task<string?> CreateBlock(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        JsonObject op,
        List<string> created)
    {
        var pageId = RequiredString(op, "pageId");
        if (await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        var block = RequiredObject<PageBlock>(op, "block");
        block.PageId = Guid.Parse(pageId);
        if (block.Id == Guid.Empty)
        {
            block.Id = Guid.NewGuid();
        }

        var createdBlock = await blocks.CreateBlockAsync(pageId, block, op["afterBlockId"]?.GetValue<string>());
        created.Add(createdBlock.Id.ToString());
        return null;
    }

    private static async Task<string?> CreateBlocks(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        JsonObject op,
        List<string> created)
    {
        var pageId = RequiredString(op, "pageId");
        if (await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        if (op["blocks"] is not JsonArray array)
        {
            return "createBlocks requires 'blocks' array.";
        }

        var payload = JsonSerializer.Deserialize<List<PageBlock>>(array.ToJsonString(), NotionMcpJson.Options) ?? [];
        foreach (var block in payload)
        {
            block.PageId = Guid.Parse(pageId);
            if (block.Id == Guid.Empty)
            {
                block.Id = Guid.NewGuid();
            }
        }

        var createdBlocks = await blocks.CreateBlocksAsync(pageId, payload, op["afterBlockId"]?.GetValue<string>());
        created.AddRange(createdBlocks.Select(b => b.Id.ToString()));
        return null;
    }

    private static async Task<string?> UpdateBlock(INotionBlockProvider blocks, JsonObject op)
    {
        var block = RequiredObject<PageBlock>(op, "block");
        await blocks.UpdateBlockAsync(block);
        return null;
    }

    private static async Task<string?> DeleteBlock(INotionBlockProvider blocks, JsonObject op)
    {
        await DeleteBlockTreeAsync(blocks, RequiredString(op, "blockId"));
        return null;
    }

    private static async Task<string?> ReorderBlocks(INotionBlockProvider blocks, JsonObject op)
        => await ReorderBlocks(null, blocks, op);

    private static async Task<string?> ReorderBlocks(
        INotionDataProvider? pages,
        INotionBlockProvider blocks,
        JsonObject op)
    {
        var pageId = RequiredString(op, "pageId");
        if (pages is not null && await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        if (op["orderedBlockIds"] is not JsonArray array)
        {
            return "reorderBlocks requires 'orderedBlockIds' array.";
        }

        var ids = array.Select(i => i?.GetValue<string>() ?? string.Empty).Where(id => id.Length > 0).ToList();
        await blocks.ReorderBlocksAsync(pageId, ids);
        return null;
    }

    private static async Task<string?> MoveBlock(INotionBlockProvider blocks, JsonObject op)
        => await MoveBlock(null, blocks, op);

    private static async Task<string?> MoveBlock(
        INotionDataProvider? pages,
        INotionBlockProvider blocks,
        JsonObject op)
    {
        MoveNotionBlockRequest request;
        if (op["request"] is JsonObject requestObject)
        {
            request = JsonSerializer.Deserialize<MoveNotionBlockRequest>(requestObject.ToJsonString(), NotionMcpJson.Options)
                ?? throw new JsonException("moveBlock request is empty.");
        }
        else
        {
            request = new MoveNotionBlockRequest(
                RequiredString(op, "blockId"),
                RequiredString(op, "targetPageId"),
                op["sourceParentBlockId"]?.GetValue<string>(),
                op["targetParentBlockId"]?.GetValue<string>(),
                op["targetIndex"]?.GetValue<int>() ?? 0);
        }

        if (pages is not null && await EnsurePageExistsAsync(pages, request.TargetPageId) is { } missing)
        {
            return missing;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetParentBlockId)
            && await IsDescendantAsync(blocks, request.BlockId, request.TargetParentBlockId))
        {
            return "moveBlock targetParentBlockId cannot be the moved block or one of its descendants.";
        }

        await blocks.MoveBlockAsync(request);
        return null;
    }

    private static async Task<string?> MoveBlockToPage(INotionBlockProvider blocks, JsonObject op)
        => await MoveBlockToPage(null, blocks, op);

    private static async Task<string?> MoveBlockToPage(
        INotionDataProvider? pages,
        INotionBlockProvider blocks,
        JsonObject op)
    {
        var targetPageId = RequiredString(op, "targetPageId");
        if (pages is not null && await EnsurePageExistsAsync(pages, targetPageId) is { } missing)
        {
            return missing;
        }

        await blocks.MoveBlockToPageAsync(
            RequiredString(op, "blockId"),
            targetPageId,
            op["afterBlockId"]?.GetValue<string>());
        return null;
    }

    private static async Task<string?> DuplicateBlock(
        INotionBlockProvider blocks,
        JsonObject op,
        List<string> created)
    {
        var duplicate = await blocks.DuplicateBlockAsync(RequiredString(op, "blockId"));
        created.Add(duplicate.Id.ToString());
        return null;
    }

    private static async Task<string?> ConvertBlockType(
        INotionBlockProvider blocks,
        JsonObject op,
        List<string> created)
    {
        var type = ParseEnum<BlockType>(RequiredString(op, "newType"));
        var converted = await blocks.ConvertBlockTypeAsync(RequiredString(op, "blockId"), type);
        created.Add(converted.Id.ToString());
        return null;
    }

    private static async Task<string?> SetPageLabels(INotionDataProvider pages, JsonObject op)
    {
        if (op["labels"] is not JsonArray labels)
        {
            return "setPageLabels requires 'labels' array.";
        }

        var pageId = RequiredString(op, "pageId");
        if (await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        await pages.SetPageLabelsAsync(
            Guid.Parse(pageId),
            labels.Select(l => l?.GetValue<string>() ?? string.Empty).Where(l => l.Length > 0).ToList());
        return null;
    }

    private static async Task<string?> ToggleFavorite(INotionDataProvider pages, JsonObject op)
    {
        var pageId = RequiredString(op, "pageId");
        if (await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        await pages.ToggleFavoriteAsync(
            pageId,
            op["isFavorite"]?.GetValue<bool>() ?? true);
        return null;
    }

    private static async Task<string?> CreateEmbeddedBlock(
        INotionDataProvider pages,
        INotionBlockProvider blocks,
        JsonObject op,
        BlockType type,
        List<string> created)
    {
        var pageId = RequiredString(op, "pageId");
        if (await EnsurePageExistsAsync(pages, pageId) is { } missing)
        {
            return missing;
        }

        var documentId = Guid.Parse(
            OptionalString(op, "documentId")
            ?? OptionalString(op, EmbeddedDocumentKey(type))
            ?? throw new InvalidOperationException($"'documentId' or '{EmbeddedDocumentKey(type)}' is required."));

        var block = new PageBlock
        {
            Id = OptionalGuid(op, "blockId") ?? Guid.NewGuid(),
            PageId = Guid.Parse(pageId),
            ParentBlockId = OptionalGuid(op, "parentBlockId"),
            Type = type,
            Content = CreateEmbeddedContent(type, documentId, op),
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

        var createdBlock = await blocks.CreateBlockAsync(pageId, block, OptionalString(op, "afterBlockId"));
        created.Add(createdBlock.Id.ToString());
        return null;
    }

    private static IBlockContent CreateEmbeddedContent(BlockType type, Guid documentId, JsonObject op)
        => type switch
        {
            BlockType.Diagram => new DiagramBlockContent
            {
                DiagramDocumentId = documentId,
                Width = OptionalInt(op, "width"),
                Height = OptionalInt(op, "height"),
                Caption = OptionalString(op, "caption")
            },
            BlockType.Wireframe => new WireframeBlockContent
            {
                WireframeDocumentId = documentId,
                Width = OptionalInt(op, "width"),
                Height = OptionalInt(op, "height"),
                Caption = OptionalString(op, "caption")
            },
            BlockType.Spreadsheet => new SpreadsheetBlockContent
            {
                SpreadsheetDocumentId = documentId,
                Width = OptionalInt(op, "width"),
                Height = OptionalInt(op, "height"),
                Caption = OptionalString(op, "caption")
            },
            _ => throw new InvalidOperationException($"Block type '{type}' is not an embedded editor block.")
        };

    internal static async Task DeleteBlockTreeAsync(INotionBlockProvider blocks, string blockId)
    {
        var children = (await blocks.GetChildBlocksAsync(blockId)).ToList();
        foreach (var child in children)
        {
            await DeleteBlockTreeAsync(blocks, child.Id.ToString());
        }

        await blocks.DeleteBlockAsync(blockId);
    }

    private static async Task<string?> EnsurePageExistsAsync(INotionDataProvider pages, string pageId)
        => await NotionPageTools.TryGetPage(pages, pageId) is null
            ? $"page '{pageId}' not found."
            : null;

    private static async Task<bool> IsDescendantAsync(INotionBlockProvider blocks, string ancestorBlockId, string candidateBlockId)
    {
        if (string.Equals(ancestorBlockId, candidateBlockId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var children = await blocks.GetChildBlocksAsync(ancestorBlockId);
        foreach (var child in children)
        {
            if (string.Equals(child.Id.ToString(), candidateBlockId, StringComparison.OrdinalIgnoreCase)
                || await IsDescendantAsync(blocks, child.Id.ToString(), candidateBlockId))
            {
                return true;
            }
        }

        return false;
    }

    private static string EmbeddedDocumentKey(BlockType type)
        => type switch
        {
            BlockType.Diagram => "diagramDocumentId",
            BlockType.Wireframe => "wireframeDocumentId",
            BlockType.Spreadsheet => "spreadsheetDocumentId",
            _ => "documentId"
        };

    private static string RequiredString(JsonObject op, string key)
        => op[key]?.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"'{key}' is required.");

    private static string? OptionalString(JsonObject op, string key)
        => op[key]?.GetValue<string>();

    private static Guid? OptionalGuid(JsonObject op, string key)
        => OptionalString(op, key) is { Length: > 0 } value ? Guid.Parse(value) : null;

    private static int? OptionalInt(JsonObject op, string key)
        => op[key]?.GetValue<int>();

    private static T RequiredObject<T>(JsonObject op, string key)
        => op[key] is JsonObject obj
            ? JsonSerializer.Deserialize<T>(obj.ToJsonString(), NotionMcpJson.Options)
                ?? throw new JsonException($"'{key}' is empty.")
            : throw new InvalidOperationException($"'{key}' object is required.");

    private static T ParseEnum<T>(string value)
        where T : struct
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"'{value}' is not a valid {typeof(T).Name}.");
}
