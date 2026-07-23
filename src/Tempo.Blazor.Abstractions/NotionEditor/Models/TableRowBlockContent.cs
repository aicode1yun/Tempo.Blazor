namespace Tempo.Blazor.NotionEditor.Models;

public class TableRowBlockContent : ITableRowBlockContent
{
    public IReadOnlyList<NotionTableCell> RichCells { get; set; } = [];
}
