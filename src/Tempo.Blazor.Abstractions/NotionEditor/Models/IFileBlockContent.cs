namespace Tempo.Blazor.NotionEditor.Models;

public interface IFileBlockContent : IMediaBlockContent
{
    string FileName { get; }
    long FileSizeBytes { get; }
    string ContentType { get; }
}
