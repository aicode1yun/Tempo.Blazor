using DocumentFormat.OpenXml;
using Tempo.Blazor.DocumentEditor.Models;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Converts document object transforms to and from DrawingML transforms.</summary>
public static class DocxTransformConverter
{
    /// <summary>Creates an a:xfrm element with dimensions, rotation, and flip flags.</summary>
    public static A.Transform2D ToTransform2D(DocumentObjectTransform transform, long cx, long cy)
    {
        var xfrm = new A.Transform2D(
            new A.Offset { X = 0L, Y = 0L },
            new A.Extents { Cx = cx, Cy = cy })
        {
            Rotation = ToRotation(transform.Rotation)
        };

        if (transform.Flip?.Horizontal == true)
        {
            xfrm.HorizontalFlip = true;
        }

        if (transform.Flip?.Vertical == true)
        {
            xfrm.VerticalFlip = true;
        }

        return xfrm;
    }

    /// <summary>Converts degrees to a DrawingML rotation value.</summary>
    public static Int32Value? ToRotation(double rotation)
        => Math.Abs(rotation) < 0.0001
            ? null
            : (Int32Value)DocxUnitConverter.DegreeToRotation(rotation);

    /// <summary>Reads rotation in degrees from the first picture transform in a drawing.</summary>
    public static double ReadRotation(W.Drawing drawing)
    {
        var rotation = drawing.Descendants<A.Transform2D>().FirstOrDefault()?.Rotation?.Value;
        return rotation.HasValue ? Math.Round(DocxUnitConverter.RotationToDegree(rotation.Value), 4) : 0;
    }

    /// <summary>Reads horizontal and vertical flip flags from the first picture transform in a drawing.</summary>
    public static DocumentObjectFlip? ReadFlip(W.Drawing drawing)
    {
        var transform = drawing.Descendants<A.Transform2D>().FirstOrDefault();
        var horizontal = transform?.HorizontalFlip?.Value == true;
        var vertical = transform?.VerticalFlip?.Value == true;
        return horizontal || vertical
            ? new DocumentObjectFlip { Horizontal = horizontal, Vertical = vertical }
            : null;
    }
}
