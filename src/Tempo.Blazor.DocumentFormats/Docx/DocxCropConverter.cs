using DocumentFormat.OpenXml.Drawing;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Converts document image crop values to and from DrawingML source rectangles.</summary>
public static class DocxCropConverter
{
    /// <summary>Returns true when the crop rectangle has no visible crop.</summary>
    public static bool IsEmpty(DocumentObjectCrop? crop)
        => crop is null
            || (Math.Abs(crop.Left) < 0.0001
                && Math.Abs(crop.Top) < 0.0001
                && Math.Abs(crop.Right) < 0.0001
                && Math.Abs(crop.Bottom) < 0.0001);

    /// <summary>Creates an a:srcRect element from normalized crop percentages.</summary>
    public static SourceRectangle? ToSourceRectangle(DocumentObjectCrop? crop)
    {
        if (IsEmpty(crop))
        {
            return null;
        }

        return new SourceRectangle
        {
            Left = DocxUnitConverter.PercentToCrop(crop!.Left),
            Top = DocxUnitConverter.PercentToCrop(crop.Top),
            Right = DocxUnitConverter.PercentToCrop(crop.Right),
            Bottom = DocxUnitConverter.PercentToCrop(crop.Bottom)
        };
    }

    /// <summary>Reads normalized crop percentages from an a:srcRect element.</summary>
    public static DocumentObjectCrop FromSourceRectangle(SourceRectangle? crop)
    {
        if (crop is null)
        {
            return new DocumentObjectCrop();
        }

        return new DocumentObjectCrop
        {
            Left = DocxUnitConverter.CropToPercent(crop.Left?.Value ?? 0),
            Top = DocxUnitConverter.CropToPercent(crop.Top?.Value ?? 0),
            Right = DocxUnitConverter.CropToPercent(crop.Right?.Value ?? 0),
            Bottom = DocxUnitConverter.CropToPercent(crop.Bottom?.Value ?? 0)
        };
    }
}
