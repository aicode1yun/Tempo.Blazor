namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class TableViewConfig : ITableViewConfig
{
    public TableRowHeight RowHeight { get; set; } = TableRowHeight.Medium;
    public bool WrapCells { get; set; }
}
