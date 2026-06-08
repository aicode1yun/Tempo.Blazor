using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotionSpaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconEmoji { get; set; }
    public string? HomePageId { get; set; }
    public NotionSpaceType Type { get; set; } = NotionSpaceType.Team;
}
