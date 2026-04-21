namespace Tempo.Blazor.NotionEditor.Models;

public class BookmarkBlockContent : IBookmarkBlockContent
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? Domain { get; set; }
    public string? Caption { get; set; }
}
