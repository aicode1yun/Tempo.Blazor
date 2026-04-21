namespace Tempo.Blazor.NotionEditor.Models;

public interface ITodoBlockContent : ITextBlockContent
{
    bool IsChecked { get; }
}
