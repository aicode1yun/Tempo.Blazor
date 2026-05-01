using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class DatabaseEndpoints
{
    public static void MapDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        var db = app.MapGroup("/api/notion/databases").WithTags("Notion Database");

        // ── Fields ────────────────────────────────────────────────────────────

        db.MapGet("/{dbId}/fields", (string dbId, MockNotionDatabaseStore store) =>
            Results.Ok(store.GetFieldsAsync(dbId).Result));

        db.MapPost("/{dbId}/fields", (string dbId, DatabaseField field, MockNotionDatabaseStore store) =>
        {
            var created = store.CreateFieldAsync(dbId, field).Result;
            return Results.Created($"/api/notion/databases/{dbId}/fields/{created.Id}", created);
        });

        db.MapPut("/{dbId}/fields/{fieldId}", (string dbId, string fieldId, DatabaseField field, MockNotionDatabaseStore store) =>
        {
            if (!Guid.TryParse(fieldId, out var fid)) return Results.BadRequest("Invalid field id");
            field.Id = fid;
            try { return Results.Ok(store.UpdateFieldAsync(dbId, field).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        db.MapDelete("/{dbId}/fields/{fieldId}", (string dbId, string fieldId, MockNotionDatabaseStore store) =>
        {
            store.DeleteFieldAsync(dbId, fieldId).Wait();
            return Results.NoContent();
        });

        db.MapPost("/{dbId}/fields/reorder", (string dbId, IEnumerable<string> orderedFieldIds, MockNotionDatabaseStore store) =>
        {
            store.ReorderFieldsAsync(dbId, orderedFieldIds).Wait();
            return Results.NoContent();
        });

        // ── Views ─────────────────────────────────────────────────────────────

        db.MapGet("/{dbId}/views", (string dbId, MockNotionDatabaseStore store) =>
            Results.Ok(store.GetViewsAsync(dbId).Result));

        db.MapPost("/{dbId}/views", (string dbId, DatabaseView view, MockNotionDatabaseStore store) =>
        {
            var created = store.CreateViewAsync(dbId, view).Result;
            return Results.Created($"/api/notion/databases/{dbId}/views/{created.Id}", created);
        });

        db.MapPut("/{dbId}/views/{viewId}", (string dbId, string viewId, DatabaseView view, MockNotionDatabaseStore store) =>
        {
            if (!Guid.TryParse(viewId, out var vid)) return Results.BadRequest("Invalid view id");
            view.Id = vid;
            try { return Results.Ok(store.UpdateViewAsync(dbId, view).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        db.MapDelete("/{dbId}/views/{viewId}", (string dbId, string viewId, MockNotionDatabaseStore store) =>
        {
            store.DeleteViewAsync(dbId, viewId).Wait();
            return Results.NoContent();
        });

        db.MapPost("/{dbId}/views/{viewId}/duplicate", (string dbId, string viewId, MockNotionDatabaseStore store) =>
        {
            try { return Results.Ok(store.DuplicateViewAsync(dbId, viewId).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── Records ───────────────────────────────────────────────────────────

        db.MapGet("/{dbId}/records", (string dbId, int page, int pageSize, MockNotionDatabaseStore store) =>
            Results.Ok(store.GetRecordsAsync(dbId, null, null, null, page, pageSize).Result));

        db.MapGet("/{dbId}/records/{recordId}", (string dbId, string recordId, MockNotionDatabaseStore store) =>
        {
            try { return Results.Ok(store.GetRecordAsync(dbId, recordId).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        db.MapPost("/{dbId}/records", (string dbId, DatabaseRecord record, MockNotionDatabaseStore store) =>
        {
            var created = store.CreateRecordAsync(dbId, record).Result;
            return Results.Created($"/api/notion/databases/{dbId}/records/{created.Id}", created);
        });

        db.MapPut("/{dbId}/records/{recordId}", (string dbId, string recordId, DatabaseRecord record, MockNotionDatabaseStore store) =>
        {
            if (!Guid.TryParse(recordId, out var rid)) return Results.BadRequest("Invalid record id");
            record.Id = rid;
            try { return Results.Ok(store.UpdateRecordAsync(dbId, record).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        db.MapDelete("/{dbId}/records/{recordId}", (string dbId, string recordId, MockNotionDatabaseStore store) =>
        {
            store.DeleteRecordAsync(dbId, recordId).Wait();
            return Results.NoContent();
        });

        db.MapPost("/{dbId}/records/batch", (string dbId, IEnumerable<DatabaseRecord> records, MockNotionDatabaseStore store) =>
            Results.Ok(store.BatchUpdateRecordsAsync(dbId, records).Result));

        db.MapPost("/{dbId}/records/{recordId}/move", (string dbId, string recordId, MoveRecordRequest request, MockNotionDatabaseStore store) =>
        {
            store.MoveRecordAsync(recordId, request.NewParentRecordId).Wait();
            return Results.NoContent();
        });

        // ── Templates ─────────────────────────────────────────────────────────

        db.MapGet("/{dbId}/templates", (string dbId, MockNotionDatabaseStore store) =>
            Results.Ok(store.GetTemplatesAsync(dbId).Result));

        db.MapPost("/{dbId}/templates", (string dbId, DatabaseRecordTemplate template, MockNotionDatabaseStore store) =>
        {
            var created = store.CreateTemplateAsync(dbId, template).Result;
            return Results.Created($"/api/notion/databases/{dbId}/templates/{created.Id}", created);
        });

        db.MapPut("/{dbId}/templates/{templateId}", (string dbId, string templateId, DatabaseRecordTemplate template, MockNotionDatabaseStore store) =>
        {
            if (!Guid.TryParse(templateId, out var tid)) return Results.BadRequest("Invalid template id");
            template.Id = tid;
            try { return Results.Ok(store.UpdateTemplateAsync(dbId, template).Result); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        db.MapDelete("/{dbId}/templates/{templateId}", (string dbId, string templateId, MockNotionDatabaseStore store) =>
        {
            store.DeleteTemplateAsync(dbId, templateId).Wait();
            return Results.NoContent();
        });

        db.MapPost("/{dbId}/templates/{templateId}/create-record", (string dbId, string templateId, MockNotionDatabaseStore store) =>
        {
            try
            {
                var record = store.CreateRecordFromTemplateAsync(dbId, templateId).Result;
                return Results.Created($"/api/notion/databases/{dbId}/records/{record.Id}", record);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── Export ────────────────────────────────────────────────────────────

        db.MapGet("/{dbId}/export", async (string dbId, string? viewId, MockNotionDatabaseStore store) =>
        {
            var stream = await store.ExportCsvAsync(dbId, viewId);
            return Results.File(stream, "text/csv", $"database-{dbId}.csv");
        });
    }
}

public record MoveRecordRequest(string? NewParentRecordId);
