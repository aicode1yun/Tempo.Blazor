using System.Net.Http.Json;
using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public class DemoNotionDatabaseProvider : INotionDatabaseProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public DemoNotionDatabaseProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    // ── Fields ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<IDatabaseField>> GetFieldsAsync(string databaseId)
    {
        var fields = await _http.GetFromJsonAsync<List<JsonElement>>($"/api/notion/databases/{databaseId}/fields");
        return fields?.Select(ReadField).ToList() ?? [];
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
        var views = await _http.GetFromJsonAsync<List<JsonElement>>($"/api/notion/databases/{databaseId}/views");
        return views?.Select(ReadView).ToList() ?? [];
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
        var request = new DatabaseRecordsQueryRequest(
            NormalizeFilter(filter),
            sorts?.ToList(),
            grouping,
            page,
            pageSize);

        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/records/query", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DatabaseRecord>>();

        if (result is null)
            return new PagedResult<IDatabaseRecord> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };

        return new PagedResult<IDatabaseRecord>
        {
            Items      = result.Items.Select(NormalizeRecord).Cast<IDatabaseRecord>().ToList(),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize
        };
    }

    private static NotionDatabaseFilter? NormalizeFilter(INotionDatabaseFilter? filter)
    {
        if (filter is null)
        {
            return null;
        }

        return new NotionDatabaseFilter
        {
            Logic = filter.Logic,
            Conditions = filter.Conditions.ToList(),
            NestedFilters = filter.NestedFilters
                .Select(NormalizeFilter)
                .Where(nested => nested is not null)
                .Cast<INotionDatabaseFilter>()
                .ToList()
        };
    }

    private static DatabaseField ReadField(JsonElement element)
    {
        var type = (DatabaseFieldType)element.GetProperty("type").GetInt32();
        var field = new DatabaseField
        {
            Id = element.GetProperty("id").GetGuid(),
            Name = element.GetProperty("name").GetString() ?? string.Empty,
            Type = type,
            IsPrimary = element.TryGetProperty("isPrimary", out var isPrimary) && isPrimary.GetBoolean(),
            IsVisible = !element.TryGetProperty("isVisible", out var isVisible) || isVisible.GetBoolean(),
            Width = element.TryGetProperty("width", out var width) && width.ValueKind != JsonValueKind.Null ? width.GetInt32() : null,
            Config = element.TryGetProperty("config", out var config) ? ReadFieldConfig(type, config) : null
        };

        return field;
    }

    private static IFieldConfig? ReadFieldConfig(DatabaseFieldType type, JsonElement config)
    {
        if (config.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            (config.ValueKind == JsonValueKind.Object && !config.EnumerateObject().Any()))
        {
            return null;
        }

        return type switch
        {
            DatabaseFieldType.Status => config.Deserialize<StatusFieldConfig>(JsonOptions),
            DatabaseFieldType.Select or DatabaseFieldType.MultiSelect => config.Deserialize<SelectFieldConfig>(JsonOptions),
            DatabaseFieldType.Date or DatabaseFieldType.DateRange or DatabaseFieldType.CreatedTime or DatabaseFieldType.LastEditedTime => config.Deserialize<DateFieldConfig>(JsonOptions),
            DatabaseFieldType.Number => config.Deserialize<NumberFieldConfig>(JsonOptions),
            DatabaseFieldType.Relation => config.Deserialize<RelationFieldConfig>(JsonOptions),
            DatabaseFieldType.Rollup => config.Deserialize<RollupFieldConfig>(JsonOptions),
            DatabaseFieldType.Formula => config.Deserialize<FormulaFieldConfig>(JsonOptions),
            _ => null
        };
    }

    private static DatabaseView ReadView(JsonElement element)
    {
        var type = (DatabaseViewType)element.GetProperty("type").GetInt32();
        return new DatabaseView
        {
            Id = element.GetProperty("id").GetGuid(),
            Name = element.GetProperty("name").GetString() ?? string.Empty,
            Type = type,
            Filter = element.TryGetProperty("filter", out var filter) && filter.ValueKind != JsonValueKind.Null
                ? filter.Deserialize<NotionDatabaseFilter>(JsonOptions)
                : null,
            Sorts = element.TryGetProperty("sorts", out var sorts) && sorts.ValueKind == JsonValueKind.Array
                ? sorts.Deserialize<List<NotionDatabaseSort>>(JsonOptions) ?? []
                : [],
            Grouping = element.TryGetProperty("grouping", out var grouping) && grouping.ValueKind != JsonValueKind.Null
                ? grouping.Deserialize<NotionDatabaseGrouping>(JsonOptions)
                : null,
            VisibleFieldIds = element.TryGetProperty("visibleFieldIds", out var visibleFieldIds) && visibleFieldIds.ValueKind == JsonValueKind.Array
                ? visibleFieldIds.Deserialize<List<Guid>>(JsonOptions) ?? []
                : [],
            Config = element.TryGetProperty("config", out var config) ? ReadViewConfig(type, config) : null
        };
    }

    private static IDatabaseViewConfig? ReadViewConfig(DatabaseViewType type, JsonElement config)
    {
        if (config.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            (config.ValueKind == JsonValueKind.Object && !config.EnumerateObject().Any()))
        {
            return null;
        }

        return type switch
        {
            DatabaseViewType.Table => config.Deserialize<TableViewConfig>(JsonOptions),
            DatabaseViewType.Board => config.Deserialize<BoardViewConfig>(JsonOptions),
            DatabaseViewType.Gallery => config.Deserialize<GalleryViewConfig>(JsonOptions),
            DatabaseViewType.Calendar => config.Deserialize<CalendarViewConfig>(JsonOptions),
            DatabaseViewType.Timeline => config.Deserialize<TimelineViewConfig>(JsonOptions),
            _ => null
        };
    }

    private static DatabaseRecord NormalizeRecord(DatabaseRecord record)
    {
        record.Fields = record.Fields.ToDictionary(pair => pair.Key, pair => NormalizeJsonValue(pair.Value));
        return record;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement json)
        {
            return value;
        }

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.TryGetDouble(out var number) ? number : json.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => json.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => json.GetRawText()
        };
    }

    public async Task<IDatabaseRecord> GetRecordAsync(string databaseId, string recordId)
    {
        var record = await _http.GetFromJsonAsync<DatabaseRecord>($"/api/notion/databases/{databaseId}/records/{recordId}");
        return record is not null
            ? NormalizeRecord(record)
            : throw new KeyNotFoundException($"Record {recordId} not found");
    }

    public async Task<IDatabaseRecord> CreateRecordAsync(string databaseId, IDatabaseRecord record)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/databases/{databaseId}/records", record);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<DatabaseRecord>()
            ?? throw new Exception("Failed to create record");
        return NormalizeRecord(created);
    }

    public async Task<IDatabaseRecord> UpdateRecordAsync(string databaseId, IDatabaseRecord record)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/databases/{databaseId}/records/{record.Id}", record);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<DatabaseRecord>()
            ?? throw new Exception("Failed to update record");
        return NormalizeRecord(updated);
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
        return updated?.Select(NormalizeRecord).Cast<IDatabaseRecord>().ToList() ?? [];
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
