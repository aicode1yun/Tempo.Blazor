namespace Tempo.Blazor.NotionEditor.Models;

public interface IHeadingBlockContent : ITextBlockContent
{
    int Level { get; }
    bool IsToggleable { get; }
}
