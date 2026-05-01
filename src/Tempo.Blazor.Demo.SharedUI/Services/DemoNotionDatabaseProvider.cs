using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public class DemoNotionDatabaseProvider : INotionDatabaseProvider
{
    private readonly HttpClient _http;

    public DemoNotionDatabaseProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    // ── Fields ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<IDatabaseField>> GetFieldsAsync(string databaseId)
    {
        var fields = await _http.GetFromJsonAsync<List<DatabaseField>>($"/api/notion/databases/{databaseId}/fields");
        return fields ?? [];
    }

    public async Task<IDatabaseField> CreateFieldAsync(string databaseId, IDatabaseField field)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/fields", field);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseField>()
            ?? throw new Exception("Failed to create field");
    }

    public async Task<IDatabaseField> UpdateFieldAsync(string databaseId, IDatabaseField field)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/databases/{databaseId}/fields/{field.Id}", field);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseField>()
            ?? throw new Exception("Failed to update field");
    }

    public async Task DeleteFieldAsync(string databaseId, string fieldId)
    {
        var response = await _http.DeleteAsync($"/api/notion/databases/{databaseId}/fields/{fieldId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task ReorderFieldsAsync(string databaseId, IEnumerable<string> orderedFieldIds)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/fields/reorder", orderedFieldIds);
        response.EnsureSuccessStatusCode();
    }

    // ── Views ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<IDatabaseView>> GetViewsAsync(string databaseId)
    {
        var views = await _http.GetFromJsonAsync<List<DatabaseView>>($"/api/notion/databases/{databaseId}/views");
        return views ?? [];
    }

    public async Task<IDatabaseView> CreateViewAsync(string databaseId, IDatabaseView view)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/views", view);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseView>()
            ?? throw new Exception("Failed to create view");
    }

    public async Task<IDatabaseView> UpdateViewAsync(string databaseId, IDatabaseView view)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/databases/{databaseId}/views/{view.Id}", view);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseView>()
            ?? throw new Exception("Failed to update view");
    }

    public async Task DeleteViewAsync(string databaseId, string viewId)
    {
        var response = await _http.DeleteAsync($"/api/notion/databases/{databaseId}/views/{viewId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<IDatabaseView> DuplicateViewAsync(string databaseId, string viewId)
    {
        var response = await _http.PostAsync($"/api/notion/databases/{databaseId}/views/{viewId}/duplicate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseView>()
            ?? throw new Exception("Failed to duplicate view");
    }

    // ── Records ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<IDatabaseRecord>> GetRecordsAsync(
        string databaseId, INotionDatabaseFilter? filter,
        IEnumerable<NotionDatabaseSort>? sorts, NotionDatabaseGrouping? grouping,
        int page, int pageSize)
    {
        var result = await _http.GetFromJsonAsync<PagedResult<DatabaseRecord>>(
            $"/api/notion/databases/{databaseId}/records?page={page}&pageSize={pageSize}");

        if (result is null)
            return new PagedResult<IDatabaseRecord> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };

        return new PagedResult<IDatabaseRecord>
        {
            Items      = result.Items.Cast<IDatabaseRecord>().ToList(),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize
        };
    }

    public async Task<IDatabaseRecord> GetRecordAsync(string databaseId, string recordId)
    {
        var record = await _http.GetFromJsonAsync<DatabaseRecord>($"/api/notion/databases/{databaseId}/records/{recordId}");
        return record ?? throw new KeyNotFoundException($"Record {recordId} not found");
    }

    public async Task<IDatabaseRecord> CreateRecordAsync(string databaseId, IDatabaseRecord record)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/records", record);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseRecord>()
            ?? throw new Exception("Failed to create record");
    }

    public async Task<IDatabaseRecord> UpdateRecordAsync(string databaseId, IDatabaseRecord record)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/databases/{databaseId}/records/{record.Id}", record);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseRecord>()
            ?? throw new Exception("Failed to update record");
    }

    public async Task DeleteRecordAsync(string databaseId, string recordId)
    {
        var response = await _http.DeleteAsync($"/api/notion/databases/{databaseId}/records/{recordId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<IDatabaseRecord>> BatchUpdateRecordsAsync(string databaseId, IEnumerable<IDatabaseRecord> records)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/records/batch", records);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<List<DatabaseRecord>>();
        return updated ?? [];
    }

    public async Task<IEnumerable<IDatabaseRecord>> GetSubItemsAsync(string parentRecordId)
        => await Task.FromResult(Enumerable.Empty<IDatabaseRecord>());

    public async Task MoveRecordAsync(string recordId, string? newParentRecordId)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/records/{recordId}/move",
            new { newParentRecordId });
        response.EnsureSuccessStatusCode();
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<IDatabaseRecordTemplate>> GetTemplatesAsync(string databaseId)
    {
        var templates = await _http.GetFromJsonAsync<List<DatabaseRecordTemplate>>($"/api/notion/databases/{databaseId}/templates");
        return templates ?? [];
    }

    public async Task<IDatabaseRecordTemplate> CreateTemplateAsync(string databaseId, IDatabaseRecordTemplate template)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/templates", template);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseRecordTemplate>()
            ?? throw new Exception("Failed to create template");
    }

    public async Task<IDatabaseRecordTemplate> UpdateTemplateAsync(string databaseId, IDatabaseRecordTemplate template)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/databases/{databaseId}/templates/{template.Id}", template);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseRecordTemplate>()
            ?? throw new Exception("Failed to update template");
    }

    public async Task DeleteTemplateAsync(string databaseId, string templateId)
    {
        var response = await _http.DeleteAsync($"/api/notion/databases/{databaseId}/templates/{templateId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<IDatabaseRecord> CreateRecordFromTemplateAsync(string databaseId, string templateId)
    {
        var response = await _http.PostAsync($"/api/notion/databases/{databaseId}/templates/{templateId}/create-record", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatabaseRecord>()
            ?? throw new Exception("Failed to create record from template");
    }

    // ── Import / Export ───────────────────────────────────────────────────────

    public async Task ImportCsvAsync(string databaseId, Stream csv)
    {
        using var content = new StreamContent(csv);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        var response = await _http.PostAsync($"/api/notion/databases/{databaseId}/import", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> ExportCsvAsync(string databaseId, string? viewId)
    {
        var url      = $"/api/notion/databases/{databaseId}/export" + (viewId != null ? $"?viewId={viewId}" : "");
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }
}
