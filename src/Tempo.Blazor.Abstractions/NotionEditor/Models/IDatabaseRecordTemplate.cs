namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public interface IDatabaseRecordTemplate
{
    Guid Id { get; }
    Guid DatabaseId { get; }
    string Name { get; }
    string? IconEmoji { get; }
    IReadOnlyDictionary<string, object?> DefaultFields { get; }
    IReadOnlyList<IPageBlock> TemplateBlocks { get; }
}
