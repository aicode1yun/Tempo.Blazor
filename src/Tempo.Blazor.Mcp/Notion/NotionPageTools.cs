using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Notion;

/// <summary>MCP page tools for NotionEditor.</summary>
[McpServerToolType]
public static class NotionPageTools
{
    [McpServerTool(Name = "notion_list_pages")]
    [Description("List NotionEditor pages. By default lists child pages under parentId (or root when omitted). Can list favorites, trash, recent pages, or pages by label. Pass scopeAppId (app GUID) when your API key grants access to more than one app, so root/favorites/recent/trash/label listings target the intended app.")]
    public static async Task<string> ListPages(
        INotionDataProvider pages,
        [Description("Optional parent page id. Omit for root pages.")] string? parentId = null,
        [Description("Optional free-text title filter applied after loading.")] string? search = null,
        [Description("Optional label filter.")] string? label = null,
        [Description("List favorites instead of children.")] bool favorites = false,
        [Description("List trash instead of children.")] bool trash = false,
        [Description("When greater than zero, list this many recent pages instead of children.")] int recent = 0,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of pages to return.")] int take = 50,
        [Description("Optional app id (GUID) scoping app-ambiguous listings (root/favorites/recent/trash/label); required when the API key grants access to more than one app.")] string? scopeAppId = null)
    {
        IEnumerable<INotionPage> result;
        if (!string.IsNullOrWhiteSpace(label))
        {
            result = await pages.GetPagesByLabelAsync(label, scopeAppId);
        }
        else if (favorites)
        {
            result = await pages.GetFavoritesAsync(scopeAppId);
        }
        else if (trash)
        {
            result = await pages.GetTrashAsync(scopeAppId);
        }
        else if (recent > 0)
        {
            result = await pages.GetRecentPagesAsync(Math.Clamp(recent, 1, 500), scopeAppId);
        }
        else
        {
            result = await pages.GetChildPagesAsync(parentId, scopeAppId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            result = result.Where(p => p.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var all = result.ToList();
        var items = all
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .ToList();

        return McpToolResults.Success(new { totalCount = all.Count, items });
    }

    [McpServerTool(Name = "notion_get_page")]
    [Description("Get Notion page metadata. Use notion_get_block_tree for canonical recursive blocks, logical table cells, concurrencyToken and digest.")]
    public static async Task<string> GetPage(
        INotionDataProvider pages,
        [Description("Page id.")] string pageId)
    {
        var page = await TryGetPage(pages, pageId);
        if (page is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Notion page '{pageId}' not found.");
        }

        return McpToolResults.Success(new { page });
    }

    [McpServerTool(Name = "notion_create_page")]
    [Description("Create a new NotionEditor page under an optional parent page. Pass scopeAppId (app GUID) for root pages when your API key grants access to more than one app.")]
    public static async Task<string> CreatePage(
        INotionDataProvider pages,
        [Description("Optional parent page id.")] string? parentId,
        [Description("New page title.")] string title,
        [Description("Optional app id (GUID) scoping a new root page; required when the API key grants access to more than one app.")] string? scopeAppId = null)
    {
        var page = await pages.CreatePageAsync(parentId, title, scopeAppId);
        return McpToolResults.Success(new { page });
    }

    [McpServerTool(Name = "notion_update_page")]
    [Description("Update a NotionEditor page. pageJson must be a NotionPage JSON object.")]
    public static async Task<string> UpdatePage(
        INotionDataProvider pages,
        [Description("NotionPage JSON object.")] string pageJson,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        var page = System.Text.Json.JsonSerializer.Deserialize<NotionPage>(pageJson, NotionMcpJson.Options);
        if (page is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The page JSON could not be parsed.");
        }

        if (await LoadPageForWrite(pages, page.Id.ToString(), expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        await pages.UpdatePageAsync(page);
        return McpToolResults.Success(new { page });
    }

    [McpServerTool(Name = "notion_delete_page")]
    [Description("Move a NotionEditor page to trash.")]
    public static async Task<string> DeletePage(
        INotionDataProvider pages,
        [Description("Page id.")] string pageId,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (await LoadPageForWrite(pages, pageId, expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        await pages.DeletePageAsync(pageId);
        return McpToolResults.Success(new { id = pageId });
    }

    [McpServerTool(Name = "notion_restore_page")]
    [Description("Restore a NotionEditor page from trash.")]
    public static async Task<string> RestorePage(
        INotionDataProvider pages,
        [Description("Page id.")] string pageId,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (await LoadPageForWrite(pages, pageId, expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        await pages.RestorePageAsync(pageId);
        return McpToolResults.Success(new { id = pageId });
    }

    [McpServerTool(Name = "notion_move_page")]
    [Description("Move a NotionEditor page under another parent page, or root when newParentId is omitted.")]
    public static async Task<string> MovePage(
        INotionDataProvider pages,
        [Description("Page id.")] string pageId,
        [Description("Optional new parent page id.")] string? newParentId = null,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (await LoadPageForWrite(pages, pageId, expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        if (!string.IsNullOrWhiteSpace(newParentId)
            && await TryGetPage(pages, newParentId) is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Notion parent page '{newParentId}' not found.");
        }

        await pages.MovePageAsync(pageId, newParentId);
        return McpToolResults.Success(new { id = pageId, parentId = newParentId });
    }

    [McpServerTool(Name = "notion_duplicate_page")]
    [Description("Duplicate a NotionEditor page and return the new page.")]
    public static async Task<string> DuplicatePage(
        INotionDataProvider pages,
        [Description("Page id.")] string pageId,
        [Description("Optional LastEditedAt value from notion_get_page for best-effort optimistic concurrency.")] DateTime? expectedLastEditedAt = null)
    {
        if (await LoadPageForWrite(pages, pageId, expectedLastEditedAt, "notion_get_page") is { } failure)
        {
            return failure;
        }

        var page = await pages.DuplicatePageAsync(pageId);
        return McpToolResults.Success(new { page });
    }

    internal static async Task<INotionPage?> TryGetPage(INotionDataProvider pages, string pageId)
    {
        try
        {
            return await pages.GetPageAsync(pageId);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    internal static async Task<string?> LoadPageForWrite(
        INotionDataProvider pages,
        string pageId,
        DateTime? expectedLastEditedAt,
        string readToolName)
    {
        var page = await TryGetPage(pages, pageId);
        if (page is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Notion page '{pageId}' not found.");
        }

        if (McpConcurrency.DateTimeConflict(expectedLastEditedAt, page.LastEditedAt, readToolName) is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        return null;
    }

}
