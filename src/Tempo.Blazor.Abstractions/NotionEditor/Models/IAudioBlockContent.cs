namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IAudioBlockContent : IMediaBlockContent
{
    AudioProvider Provider { get; }
}
