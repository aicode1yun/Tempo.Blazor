namespace Tempo.Blazor.NotionEditor.Models;

public class LinkedPageBlockContent : ILinkedPageBlockContent
{
    public Guid LinkedPageId { get; set; }
    public string? Title { get; set; }
    public string? IconEmoji { get; set; }
}
