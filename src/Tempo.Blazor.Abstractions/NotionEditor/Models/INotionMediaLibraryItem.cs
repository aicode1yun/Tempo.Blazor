namespace Tempo.Blazor.NotionEditor.Models;

public interface INotionMediaLibraryItem
{
    string    Id            { get; }
    string    Name          { get; }
    string    Url           { get; }
    string?   ThumbnailUrl  { get; }
    string    ContentType   { get; }
    long?     FileSizeBytes { get; }
    DateTime? CreatedAt     { get; }
}
