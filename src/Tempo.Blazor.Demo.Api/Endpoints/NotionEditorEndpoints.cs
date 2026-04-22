using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class NotionEditorEndpoints
{
    public static void MapNotionEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var pageGroup = app.MapGroup("/api/notion/pages").WithTags("Notion Editor");
        var blockGroup = app.MapGroup("/api/notion/blocks").WithTags("Notion Editor");

        // Pages endpoints
        pageGroup.MapGet("/{pageId}", (string pageId, MockNotionDataStore store) =>
        {
            try
            {
                var page = store.GetPageAsync(pageId).Result;
                return Results.Ok(page);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        pageGroup.MapGet("/", (MockNotionDataStore store) =>
        {
            var pages = store.GetAllPages();
            return Results.Ok(pages);
        });

        pageGroup.MapGet("/{parentId}/children", (string parentId, MockNotionDataStore store) =>
        {
            var children = store.GetChildPagesAsync(parentId).Result;
            return Results.Ok(children);
        });

        pageGroup.MapGet("/favorites", (MockNotionDataStore store) =>
        {
            var favorites = store.GetFavoritesAsync().Result;
            return Results.Ok(favorites);
        });

        pageGroup.MapGet("/recent/{count}", (int count, MockNotionDataStore store) =>
        {
            var recent = store.GetRecentPagesAsync(count).Result;
            return Results.Ok(recent);
        });

        pageGroup.MapPost("/", (CreatePageRequest request, MockNotionDataStore store) =>
        {
            var page = store.CreatePageAsync(request.ParentId, request.Title).Result;
            return Results.Created($"/api/notion/pages/{page.Id}", page);
        });

        pageGroup.MapPut("/{pageId}", (string pageId, UpdatePageRequest request, MockNotionDataStore store) =>
        {
            try
            {
                var page = store.GetPageAsync(pageId).Result;
                if (page is NotionPage notionPage)
                {
                    notionPage.Title = request.Title ?? notionPage.Title;
                    notionPage.Description = request.Description ?? notionPage.Description;
                    notionPage.IconEmoji = request.IconEmoji ?? notionPage.IconEmoji;
                    store.UpdatePageAsync(notionPage).Wait();
                    return Results.Ok(notionPage);
                }
                return Results.BadRequest("Invalid page type");
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        pageGroup.MapDelete("/{pageId}", (string pageId, MockNotionDataStore store) =>
        {
            store.DeletePageAsync(pageId).Wait();
            return Results.NoContent();
        });

        pageGroup.MapPost("/{pageId}/favorite/{isFavorite}", (string pageId, bool isFavorite, MockNotionDataStore store) =>
        {
            store.ToggleFavoriteAsync(pageId, isFavorite).Wait();
            return Results.NoContent();
        });

        // Blocks endpoints
        blockGroup.MapGet("/page/{pageId}", (string pageId, MockNotionBlockStore store) =>
        {
            var blocks = store.GetBlocksAsync(pageId).Result;
            return Results.Ok(blocks);
        });

        blockGroup.MapGet("/parent/{parentBlockId}", (string parentBlockId, MockNotionBlockStore store) =>
        {
            var children = store.GetChildBlocksAsync(parentBlockId).Result;
            return Results.Ok(children);
        });

        blockGroup.MapPost("/", (CreateBlockRequest request, MockNotionBlockStore store) =>
        {
            try
            {
                var block = store.CreateBlockAsync(request.PageId, request.Block, request.AfterBlockId).Result;
                return Results.Created($"/api/notion/blocks/{block.Id}", block);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        blockGroup.MapPut("/{blockId}", (string blockId, UpdateBlockRequest request, MockNotionBlockStore store) =>
        {
            try
            {
                // This is a simplified implementation - in real app you'd fetch and update
                return Results.Ok();
            }
            catch
            {
                return Results.NotFound();
            }
        });

        blockGroup.MapDelete("/{blockId}", (string blockId, MockNotionBlockStore store) =>
        {
            store.DeleteBlockAsync(blockId).Wait();
            return Results.NoContent();
        });

        blockGroup.MapPost("/reorder", (ReorderBlocksRequest request, MockNotionBlockStore store) =>
        {
            store.ReorderBlocksAsync(request.PageId, request.OrderedBlockIds).Wait();
            return Results.NoContent();
        });

        blockGroup.MapPost("/{blockId}/duplicate", (string blockId, MockNotionBlockStore store) =>
        {
            try
            {
                var duplicated = store.DuplicateBlockAsync(blockId).Result;
                return Results.Created($"/api/notion/blocks/{duplicated.Id}", duplicated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });
    }
}

public record CreatePageRequest(string Title, string? ParentId = null);
public record UpdatePageRequest(string? Title, string? Description, string? IconEmoji);
public record CreateBlockRequest(string PageId, IPageBlock Block, string? AfterBlockId = null);
public record UpdateBlockRequest(string Content);
public record ReorderBlocksRequest(string PageId, IEnumerable<string> OrderedBlockIds);
