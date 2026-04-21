namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionDatabaseProvider
{
    Task<IEnumerable<IDatabaseField>> GetFieldsAsync(string databaseId);
    Task<IDatabaseField> CreateFieldAsync(string databaseId, IDatabaseField field);
    Task<IDatabaseField> UpdateFieldAsync(string databaseId, IDatabaseField field);
    Task DeleteFieldAsync(string databaseId, string fieldId);
    Task ReorderFieldsAsync(string databaseId, IEnumerable<string> orderedFieldIds);

    Task<IEnumerable<IDatabaseView>> GetViewsAsync(string databaseId);
    Task<IDatabaseView> CreateViewAsync(string databaseId, IDatabaseView view);
    Task<IDatabaseView> UpdateViewAsync(string databaseId, IDatabaseView view);
    Task DeleteViewAsync(string databaseId, string viewId);
    Task<IDatabaseView> DuplicateViewAsync(string databaseId, string viewId);

    Task<PagedResult<IDatabaseRecord>> GetRecordsAsync(string databaseId, INotionDatabaseFilter? filter, IEnumerable<NotionDatabaseSort>? sorts, NotionDatabaseGrouping? grouping, int page, int pageSize);
    Task<IDatabaseRecord> GetRecordAsync(string databaseId, string recordId);
    Task<IDatabaseRecord> CreateRecordAsync(string databaseId, IDatabaseRecord record);
    Task<IDatabaseRecord> UpdateRecordAsync(string databaseId, IDatabaseRecord record);
    Task DeleteRecordAsync(string databaseId, string recordId);
    Task<IEnumerable<IDatabaseRecord>> BatchUpdateRecordsAsync(string databaseId, IEnumerable<IDatabaseRecord> records);

    Task<IEnumerable<IDatabaseRecord>> GetSubItemsAsync(string parentRecordId);
    Task MoveRecordAsync(string recordId, string? newParentRecordId);

    Task<IEnumerable<IDatabaseRecordTemplate>> GetTemplatesAsync(string databaseId);
    Task<IDatabaseRecordTemplate> CreateTemplateAsync(string databaseId, IDatabaseRecordTemplate template);
    Task<IDatabaseRecordTemplate> UpdateTemplateAsync(string databaseId, IDatabaseRecordTemplate template);
    Task DeleteTemplateAsync(string databaseId, string templateId);
    Task<IDatabaseRecord> CreateRecordFromTemplateAsync(string databaseId, string templateId);

    Task ImportCsvAsync(string databaseId, Stream csv);
    Task<Stream> ExportCsvAsync(string databaseId, string? viewId);
}
