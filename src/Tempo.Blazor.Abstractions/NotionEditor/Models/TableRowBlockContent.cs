namespace Tempo.Blazor.NotionEditor.Models;

public class TableRowBlockContent : ITableRowBlockContent
{
    public IReadOnlyList<string> Cells { get; set; } = new List<string>();
    public IReadOnlyList<NotionTableCell> RichCells { get; set; } = [];
}
