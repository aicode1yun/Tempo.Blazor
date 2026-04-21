namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public class DatabaseRecordTemplate : IDatabaseRecordTemplate
{
    public Guid Id { get; set; }
    public Guid DatabaseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconEmoji { get; set; }
    public IReadOnlyDictionary<string, object?> DefaultFields { get; set; } = new Dictionary<string, object?>();
    public IReadOnlyList<IPageBlock> TemplateBlocks { get; set; } = new List<IPageBlock>();
}
