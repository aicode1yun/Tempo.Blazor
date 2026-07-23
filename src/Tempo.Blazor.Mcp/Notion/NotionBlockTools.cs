using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
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
    [Description("Atomically apply a strict JSON operation array. Supported op values: createBlock, createBlocks, createTable, patchBlockContent, moveBlock, reorderBlocks, convertBlockType, deleteBlock, replaceBlocks. Every request requires a stable idempotencyKey. Legacy aliases and unknown fields are rejected.")]
    public static async Task<string> ApplyBlockOperations(
        IServiceProvider services,
        INotionAggregateProvider? provider,
        [Description("Stable idempotency key for this logical request. Reusing it with the same canonical request replays the original result; a different request is rejected.")] string idempotencyKey,
        [Description("Strict JSON array of operations. Each item uses the 'op' discriminator and may include clientRef for created/updated/deleted ID mapping.")] string operationsJson,
        [Description("Optional JSON array of {pageId, concurrencyToken} values from the latest aggregate read. Stale tokens reject the whole request before application.")] string expectedPageVersionsJson = "[]",
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "The host has not registered INotionAggregateProvider.");
        }
        var receipts = services.GetService<InMemoryNotionIdempotencyReceiptStore>();
        if (receipts is null)
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "The host must call AddTempoNotionMcpTools to register the authoring runtime.");
        }

        if (!TryParseExpectedVersions(
                expectedPageVersionsJson,
                out var expectedVersions,
                out var parseIssue))
        {
            return Serialize(new NotionAtomicAuthoringResult
            {
                Errors = [parseIssue!]
            });
        }

        var engine = new NotionAtomicAuthoringEngine(
            provider,
            new NotionStrictOperationCompiler(),
            receipts);
        var result = await engine.ExecuteAsync(
            new NotionAtomicAuthoringRequest
            {
                IdempotencyKey = idempotencyKey,
                OperationsJson = operationsJson,
                ExpectedPageVersions = expectedVersions
            },
            cancellationToken);
        return Serialize(result);
    }

    private static bool TryParseExpectedVersions(
        string json,
        out IReadOnlyList<NotionExpectedPageVersion> versions,
        out NotionAggregateIssue? issue)
    {
        versions = [];
        issue = null;
        JsonArray? array;
        try
        {
            array = JsonNode.Parse(json) as JsonArray;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            array = null;
        }
        if (array is null)
        {
            issue = Error(
                "expected_versions_invalid",
                "expectedPageVersionsJson must contain a JSON array.",
                "$.expectedPageVersions");
            return false;
        }

        var parsed = new List<NotionExpectedPageVersion>();
        for (var index = 0; index < array.Count; index++)
        {
            var path = $"$.expectedPageVersions[{index}]";
            if (array[index] is not JsonObject item)
            {
                issue = Error(
                    "expected_version_must_be_object",
                    "Each expected page version must be an object.",
                    path);
                return false;
            }
            var unknown = item.Select(property => property.Key)
                .FirstOrDefault(name => name is not ("pageId" or "concurrencyToken"));
            if (unknown is not null)
            {
                issue = Error(
                    "unknown_field",
                    $"Unknown field '{unknown}'.",
                    $"{path}.{unknown}");
                return false;
            }
            if (item["pageId"] is not JsonValue pageValue ||
                !pageValue.TryGetValue<string>(out var pageText) ||
                !Guid.TryParse(pageText, out var pageId) ||
                pageId == Guid.Empty)
            {
                issue = Error(
                    "guid_required",
                    "pageId must be a non-empty GUID string.",
                    $"{path}.pageId");
                return false;
            }
            if (item["concurrencyToken"] is not JsonValue tokenValue ||
                !tokenValue.TryGetValue<string>(out var token) ||
                string.IsNullOrWhiteSpace(token))
            {
                issue = Error(
                    "concurrency_token_required",
                    "concurrencyToken must be a non-empty string.",
                    $"{path}.concurrencyToken");
                return false;
            }

            parsed.Add(new NotionExpectedPageVersion(pageId, token));
        }

        versions = parsed;
        return true;
    }

    private static string Serialize(NotionAtomicAuthoringResult result)
        => JsonSerializer.Serialize(result, McpJson.Options);

    private static NotionAggregateIssue Error(string code, string message, string path)
        => new()
        {
            Code = code,
            Severity = NotionIssueSeverity.Error,
            Message = message,
            Path = path
        };
}
