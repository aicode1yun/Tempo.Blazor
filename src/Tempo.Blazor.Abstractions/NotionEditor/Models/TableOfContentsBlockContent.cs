namespace Tempo.Blazor.NotionEditor.Models;

public class TableOfContentsBlockContent : ITableOfContentsBlockContent
{
    public int MaxLevel { get; set; } = 3;
}
