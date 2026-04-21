namespace Tempo.Blazor.NotionEditor.Models;

public class ChildPageBlockContent : IChildPageBlockContent
{
    public Guid ChildPageId { get; set; }
    public string? Title { get; set; }
    public string? IconEmoji { get; set; }
}
