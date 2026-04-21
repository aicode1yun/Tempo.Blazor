namespace Tempo.Blazor.NotionEditor.Models;

public interface ILinkedPageBlockContent : IBlockContent
{
    Guid LinkedPageId { get; }
    string? Title { get; }
    string? IconEmoji { get; }
}
