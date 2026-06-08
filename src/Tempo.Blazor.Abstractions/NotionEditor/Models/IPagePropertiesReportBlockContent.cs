namespace Tempo.Blazor.NotionEditor.Models;

public interface IPagePropertiesReportBlockContent : IBlockContent
{
    IReadOnlyList<string> Labels { get; }
    IReadOnlyList<string> Columns { get; }
}
