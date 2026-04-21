namespace Tempo.Blazor.NotionEditor.Models;

public class DatabaseRecord : IDatabaseRecord
{
    public Guid Id { get; set; }
    public Guid DatabaseId { get; set; }
    public Guid? ParentRecordId { get; set; }
    public IReadOnlyDictionary<string, object?> Fields { get; set; } = new Dictionary<string, object?>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime LastEditedAt { get; set; } = DateTime.UtcNow;
    public string? LastEditedByUserId { get; set; }
}
