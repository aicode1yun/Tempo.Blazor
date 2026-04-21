namespace Tempo.Blazor.NotionEditor.Models;

public class ColumnBlockContent : IColumnBlockContent
{
    public int ColumnIndex { get; set; }
    public double WidthPercent { get; set; }
}
