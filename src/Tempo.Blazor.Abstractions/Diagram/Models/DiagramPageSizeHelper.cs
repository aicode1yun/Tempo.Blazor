namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Helper for converting <see cref="DiagramPageSize"/> to pixel dimensions at 96 DPI.</summary>
public static class DiagramPageSizeHelper
{
    private const double Dpi = 96.0;
    private const double MmPerInch = 25.4;

    /// <summary>Returns the default width and height for the given page size and orientation.</summary>
    public static (double Width, double Height) GetDimensions(DiagramPageSize size, DiagramPageOrientation orientation)
    {
        var (w, h) = GetPortraitDimensions(size);
        return orientation == DiagramPageOrientation.Landscape ? (h, w) : (w, h);
    }

    private static (double Width, double Height) GetPortraitDimensions(DiagramPageSize size)
    {
        return size switch
        {
            DiagramPageSize.A4 => (MmToPx(210), MmToPx(297)),
            DiagramPageSize.A3 => (MmToPx(297), MmToPx(420)),
            DiagramPageSize.Letter => (InchToPx(8.5), InchToPx(11)),
            _ => (3000, 2000),
        };
    }

    private static double MmToPx(double mm) => mm / MmPerInch * Dpi;
    private static double InchToPx(double inch) => inch * Dpi;
}
