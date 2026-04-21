namespace Tempo.Blazor.NotionEditor.Models;

public interface IListBlockContent : ITextBlockContent
{
    int IndentLevel { get; }
}
