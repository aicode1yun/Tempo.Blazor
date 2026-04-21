namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableBlockContent : IBlockContent
{
    bool HasHeaderRow { get; }
    bool HasHeaderColumn { get; }
    int ColumnCount { get; }
}
