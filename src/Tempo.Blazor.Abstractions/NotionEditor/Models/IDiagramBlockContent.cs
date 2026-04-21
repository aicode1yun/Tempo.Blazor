namespace Tempo.Blazor.NotionEditor.Models;

public interface IDiagramBlockContent : IBlockContent
{
    Guid DiagramDocumentId { get; }
    string? SvgPreviewCache { get; }
    int? Width { get; }
    int? Height { get; }
    string? Caption { get; }
}
