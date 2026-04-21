namespace Tempo.Blazor.NotionEditor.Models;

public interface IDatabaseRecord
{
    Guid Id { get; }
    Guid DatabaseId { get; }
    Guid? ParentRecordId { get; }
    IReadOnlyDictionary<string, object?> Fields { get; }
    DateTime CreatedAt { get; }
    string? CreatedByUserId { get; }
    DateTime LastEditedAt { get; }
    string? LastEditedByUserId { get; }
}
