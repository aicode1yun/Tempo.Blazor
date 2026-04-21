namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IVideoBlockContent : IMediaBlockContent
{
    VideoProvider Provider { get; }
}
