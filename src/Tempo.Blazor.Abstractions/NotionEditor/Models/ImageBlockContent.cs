namespace Tempo.Blazor.NotionEditor.Models;

public class ImageBlockContent : IImageBlockContent
{
    public string? AltText { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? Caption { get; set; }
    public int? Width { get; set; }
}
