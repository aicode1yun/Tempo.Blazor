namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class VideoBlockContent : IVideoBlockContent
{
    public VideoProvider Provider { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? Caption { get; set; }
    public int? Width { get; set; }
}
