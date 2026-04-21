namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface ITableViewConfig : IDatabaseViewConfig
{
    TableRowHeight RowHeight { get; }
    bool WrapCells { get; }
}
