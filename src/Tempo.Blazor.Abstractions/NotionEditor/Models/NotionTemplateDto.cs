namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public sealed class NotionTemplateDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconEmoji { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public IReadOnlyList<PageBlock> Blocks { get; set; } = [];
}
