namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableRowBlockContent : IBlockContent
{
    IReadOnlyList<string> Cells { get; }
}
