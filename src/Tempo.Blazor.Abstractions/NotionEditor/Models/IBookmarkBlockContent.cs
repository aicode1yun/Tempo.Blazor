namespace Tempo.Blazor.NotionEditor.Models;

public interface IBookmarkBlockContent : IBlockContent
{
    string Url { get; }
    string? Title { get; }
    string? Description { get; }
    string? CoverImageUrl { get; }
    string? FaviconUrl { get; }
    string? Domain { get; }
    string? Caption { get; }
}
