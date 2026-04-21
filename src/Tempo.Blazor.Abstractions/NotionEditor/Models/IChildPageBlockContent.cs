namespace Tempo.Blazor.NotionEditor.Models;

public interface IChildPageBlockContent : IBlockContent
{
    Guid ChildPageId { get; }
    string? Title { get; }
    string? IconEmoji { get; }
}
