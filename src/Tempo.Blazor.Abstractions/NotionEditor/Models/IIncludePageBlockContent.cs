namespace Tempo.Blazor.NotionEditor.Models;

public interface IIncludePageBlockContent : IBlockContent
{
    Guid? SourcePageId { get; }
}
