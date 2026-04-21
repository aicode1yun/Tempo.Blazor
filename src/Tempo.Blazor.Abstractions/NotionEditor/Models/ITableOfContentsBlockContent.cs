namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableOfContentsBlockContent : IBlockContent
{
    int MaxLevel { get; }
}
