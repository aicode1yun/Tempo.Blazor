using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class NotionEditorEndpoints
{
    public static void MapNotionEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var pageGroup = app.MapGroup("/api/notion/pages").WithTags("Notion Editor");
        var blockGroup = app.MapGroup("/api/notion/blocks").WithTags("Notion Editor");

        // ── Page CRUD ─────────────────────────────────────────────────────────

        pageGroup.MapGet("/", (MockNotionDataStore store) =>
            Results.Ok(store.GetAllPages()));

        // Literal routes must be registered before parameterised {pageId}
        pageGroup.MapGet("/root/children", (MockNotionDataStore store) =>
            Results.Ok(store.GetChildPagesAsync(null).Result));

        pageGroup.MapGet("/favorites", (MockNotionDataStore store) =>
            Results.Ok(store.GetFavoritesAsync().Result));

        pageGroup.MapGet("/recent/{count}", (int count, MockNotionDataStore store) =>
            Results.Ok(store.GetRecentPagesAsync(count).Result));

        pageGroup.MapGet("/trash", (MockNotionDataStore store) =>
            Results.Ok(store.GetTrashAsync().Result));

        pageGroup.MapGet("/{pageId}", (string pageId, MockNotionDataStore store) =>
        {
            try   { return Results.Ok(store.GetPageAsync(pageId).Result); }
            catch { return Results.NotFound(); }
        });

        pageGroup.MapGet("/{parentId}/children", (string parentId, MockNotionDataStore store) =>
            Results.Ok(store.GetChildPagesAsync(parentId).Result));

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
                    notionPage.Title        = request.Title        ?? notionPage.Title;
                    notionPage.Description  = request.Description  ?? notionPage.Description;
                    notionPage.IconEmoji    = request.IconEmoji    ?? notionPage.IconEmoji;
                    notionPage.IsFullWidth  = request.IsFullWidth  ?? notionPage.IsFullWidth;
                    notionPage.IsSmallText  = request.IsSmallText  ?? notionPage.IsSmallText;
                    notionPage.IsLocked     = request.IsLocked     ?? notionPage.IsLocked;
                    store.UpdatePageAsync(notionPage).Wait();
                    return Results.Ok(notionPage);
                }
                return Results.BadRequest();
            }
            catch { return Results.NotFound(); }
        });

        pageGroup.MapDelete("/{pageId}", (string pageId, MockNotionDataStore store) =>
        {
            store.DeletePageAsync(pageId).Wait();
            return Results.NoContent();
        });

        pageGroup.MapPost("/{pageId}/restore", (string pageId, MockNotionDataStore store) =>
        {
            store.RestorePageAsync(pageId).Wait();
            return Results.NoContent();
        });

        pageGroup.MapPost("/{pageId}/move", (string pageId, MovePageRequest req, MockNotionDataStore store) =>
        {
            store.MovePageAsync(pageId, req.NewParentId).Wait();
            return Results.NoContent();
        });

        pageGroup.MapPost("/{pageId}/duplicate", (string pageId, MockNotionDataStore store) =>
        {
            try
            {
                var dup = store.DuplicatePageAsync(pageId).Result;
                return Results.Created($"/api/notion/pages/{dup.Id}", dup);
            }
            catch { return Results.NotFound(); }
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

        blockGroup.MapPost("/batch", (BatchCreateBlocksRequest request, MockNotionBlockStore store) =>
        {
            try
            {
                var created = store.CreateBlocksAsync(request.PageId, request.Blocks, request.AfterBlockId).Result;
                return Results.Ok(created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        blockGroup.MapPut("/{blockId}", async (string blockId, PageBlock request, MockNotionBlockStore store) =>
        {
            if (!Guid.TryParse(blockId, out var id))
                return Results.BadRequest("Invalid block id");
            request.Id = id;
            await store.UpdateBlockAsync(request);
            return Results.Ok(request);
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

        blockGroup.MapPost("/{blockId}/convert", (string blockId, ConvertBlockRequest request, MockNotionBlockStore store) =>
        {
            try
            {
                var converted = store.ConvertBlockTypeAsync(blockId, request.NewType).Result;
                return Results.Ok(converted);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // ── Spreadsheet documents ─────────────────────────────────────────────

        var spreadsheetGroup = app.MapGroup("/api/notion/spreadsheets").WithTags("Notion Editor");

        spreadsheetGroup.MapPost("/", (MockSpreadsheetDocumentStore store) =>
        {
            var (id, workbook) = store.Create();
            return Results.Ok(new { id, workbook });
        });

        spreadsheetGroup.MapGet("/{id}", (Guid id, MockSpreadsheetDocumentStore store) =>
        {
            var workbook = store.Get(id);
            return workbook is null ? Results.NotFound() : Results.Ok(workbook);
        });

        spreadsheetGroup.MapPut("/{id}", (Guid id, SpreadsheetWorkbook workbook, MockSpreadsheetDocumentStore store) =>
            Results.Ok(store.Save(id, workbook)));

        // ── Reset (for E2E tests) ─────────────────────────────────────────────
        app.MapPost("/api/notion/reset", (MockNotionDataStore dataStore, MockNotionBlockStore blockStore) =>
        {
            dataStore.Reset();
            blockStore.Reset();
            return Results.NoContent();
        });
    }
}

public record CreatePageRequest(string Title, string? ParentId = null);
public record UpdatePageRequest(string? Title, string? Description, string? IconEmoji,
    bool? IsFullWidth = null, bool? IsSmallText = null, bool? IsLocked = null);
public record MovePageRequest(string? NewParentId);
public record CreateBlockRequest(string PageId, PageBlock Block, string? AfterBlockId = null);
public record ReorderBlocksRequest(string PageId, IEnumerable<string> OrderedBlockIds);
public record ConvertBlockRequest(BlockType NewType);
public record BatchCreateBlocksRequest(string PageId, IEnumerable<PageBlock> Blocks, string? AfterBlockId = null);
