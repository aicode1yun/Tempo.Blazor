namespace Tempo.Blazor.NotionEditor.Models;

public class WireframeBlockContent : IWireframeBlockContent
{
    public Guid WireframeDocumentId { get; set; }
    public string? SvgPreviewCache { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Caption { get; set; }
}
