namespace Tempo.Blazor.NotionEditor.Models;

public class PdfBlockContent : IPdfBlockContent
{
    public string Url { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? Caption { get; set; }
    public int? Width { get; set; }
}
