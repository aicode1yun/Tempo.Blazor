namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class GalleryViewConfig : IGalleryViewConfig
{
    public GalleryCardSize CardSize { get; set; } = GalleryCardSize.Medium;
    public Guid? CoverFieldId { get; set; }
    public CoverFit CoverFit { get; set; } = CoverFit.Cover;
}
