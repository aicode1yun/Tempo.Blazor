namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class ImageBlockContent : IImageBlockContent
{
    public string? AltText { get; set; }
    public MediaAlignment Alignment { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? Caption { get; set; }
    public int? Width { get; set; }
}
