namespace Tempo.Blazor.NotionEditor.Models;

public interface IPagePropertiesBlockContent : IBlockContent
{
    IReadOnlyList<PagePropertyRow> Rows { get; }
}
