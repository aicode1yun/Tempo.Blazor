namespace Tempo.Blazor.NotionEditor.Models;

public interface IWireframeBlockContent : IBlockContent
{
    Guid WireframeDocumentId { get; }
    string? SvgPreviewCache { get; }
    int? Width { get; }
    int? Height { get; }
    string? Caption { get; }
}
