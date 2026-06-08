namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableBlockContent : IBlockContent
{
    bool HasHeaderRow { get; }
    bool HasHeaderColumn { get; }
    bool HasColumnHeader { get; }
    bool HasRowHeader { get; }
    int ColumnCount { get; }
}
