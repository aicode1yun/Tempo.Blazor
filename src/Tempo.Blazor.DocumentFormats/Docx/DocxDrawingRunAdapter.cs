using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Docx;

internal static class DocxDrawingRunAdapter
{
    public static DocumentDrawingRun FromImageBlock(ImageBlockContent image)
        => new()
        {
            Source = image.Source,
            Url = image.Url,
            AssetId = image.AssetId,
            AltText = image.AltText,
            IsDecorative = image.IsDecorative,
            Caption = image.Caption,
            Size = image.Size,
            NaturalSize = image.NaturalSize,
            Layout = image.Layout,
            LinkUrl = image.LinkUrl
        };
}
