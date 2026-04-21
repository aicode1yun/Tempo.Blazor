namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IGalleryViewConfig : IDatabaseViewConfig
{
    GalleryCardSize CardSize { get; }
    Guid? CoverFieldId { get; }
    CoverFit CoverFit { get; }
}
