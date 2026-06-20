namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotionMediaLibraryItem : INotionMediaLibraryItem
{
    public string    Id            { get; set; } = string.Empty;
    public string    Name          { get; set; } = string.Empty;
    public string    Url           { get; set; } = string.Empty;
    public string?   ThumbnailUrl  { get; set; }
    public string    ContentType   { get; set; } = string.Empty;
    public long?     FileSizeBytes { get; set; }
    public DateTime? CreatedAt     { get; set; }
}
