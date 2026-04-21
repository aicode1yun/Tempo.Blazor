namespace Tempo.Blazor.NotionEditor.Models;

public class DiagramBlockContent : IDiagramBlockContent
{
    public Guid DiagramDocumentId { get; set; }
    public string? SvgPreviewCache { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Caption { get; set; }
}
