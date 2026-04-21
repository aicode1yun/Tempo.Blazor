namespace Tempo.Blazor.NotionEditor.Models;

public interface ICodeBlockContent : IBlockContent
{
    string Code { get; }
    string? Language { get; }
    bool ShowLineNumbers { get; }
    string? Caption { get; }
    bool WrapLines { get; }
}
