namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableRowBlockContent : IBlockContent
{
    IReadOnlyList<NotionTableCell> RichCells { get; }
}
