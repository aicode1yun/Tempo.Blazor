namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IImageBlockContent : IMediaBlockContent
{
    string? AltText { get; }
    MediaAlignment Alignment { get; }
}
