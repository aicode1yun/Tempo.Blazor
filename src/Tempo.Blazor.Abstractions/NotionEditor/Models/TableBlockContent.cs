namespace Tempo.Blazor.NotionEditor.Models;

public class TableBlockContent : ITableBlockContent
{
    public bool HasHeaderRow { get; set; }
    public bool HasHeaderColumn { get; set; }
    public int ColumnCount { get; set; }
}
