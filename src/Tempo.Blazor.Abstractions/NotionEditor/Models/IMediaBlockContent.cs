namespace Tempo.Blazor.NotionEditor.Models;

public interface IMediaBlockContent : IBlockContent
{
    string Url { get; }
    string? FileId { get; }
    string? Caption { get; }
    int? Width { get; }
}
