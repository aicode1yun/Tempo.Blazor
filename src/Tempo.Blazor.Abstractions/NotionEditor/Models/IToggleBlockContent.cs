namespace Tempo.Blazor.NotionEditor.Models;

public interface IToggleBlockContent : ITextBlockContent
{
    bool IsOpen { get; }
}
