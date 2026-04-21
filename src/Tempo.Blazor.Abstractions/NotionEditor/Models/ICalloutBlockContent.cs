namespace Tempo.Blazor.NotionEditor.Models;

public interface ICalloutBlockContent : ITextBlockContent
{
    string? IconEmoji { get; }
    string? IconImageUrl { get; }
}
