namespace Tempo.Blazor.NotionEditor.Models;

public interface IColumnBlockContent : IBlockContent
{
    int ColumnIndex { get; }
    double WidthPercent { get; }
}
