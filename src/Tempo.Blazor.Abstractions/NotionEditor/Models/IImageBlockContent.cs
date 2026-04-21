namespace Tempo.Blazor.NotionEditor.Models;

public interface IImageBlockContent : IMediaBlockContent
{
    string? AltText { get; }
}
