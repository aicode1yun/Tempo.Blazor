namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Centralized unit conversions used by DOCX WordprocessingML and DrawingML import/export.</summary>
public static class DocxUnitConverter
{
    /// <summary>EMU units per inch.</summary>
    public const long EmusPerInch = 914400L;

    /// <summary>Points per inch.</summary>
    public const double PointsPerInch = 72d;

    /// <summary>EMU units per point.</summary>
    public const double EmusPerPoint = EmusPerInch / PointsPerInch;

    /// <summary>Twips per point.</summary>
    public const double TwipsPerPoint = 20d;

    /// <summary>DrawingML rotation units per degree.</summary>
    public const int RotationUnitsPerDegree = 60000;

    /// <summary>DrawingML crop units per percent.</summary>
    public const int CropUnitsPerPercent = 1000;

    /// <summary>Converts points to EMUs.</summary>
    public static long PointToEmu(double points)
        => (long)Math.Round(points * EmusPerPoint);

    /// <summary>Converts EMUs to points.</summary>
    public static double EmuToPoint(long emus)
        => emus / EmusPerPoint;

    /// <summary>Converts inches to EMUs.</summary>
    public static long InchToEmu(double inches)
        => (long)Math.Round(inches * EmusPerInch);

    /// <summary>Converts pixels to EMUs using the supplied DPI.</summary>
    public static long PixelToEmu(double pixels, double dpi = 96d)
        => dpi <= 0 ? 0 : InchToEmu(pixels / dpi);

    /// <summary>Converts degrees to DrawingML rotation units.</summary>
    public static int DegreeToRotation(double degrees)
        => (int)Math.Round(degrees * RotationUnitsPerDegree);

    /// <summary>Converts DrawingML rotation units to degrees.</summary>
    public static double RotationToDegree(int rotation)
        => rotation / (double)RotationUnitsPerDegree;

    /// <summary>Converts a percent crop value to DrawingML crop units.</summary>
    public static int PercentToCrop(double percent)
        => (int)Math.Round(percent * CropUnitsPerPercent);

    /// <summary>Converts DrawingML crop units to a percent crop value.</summary>
    public static double CropToPercent(int crop)
        => crop / (double)CropUnitsPerPercent;

    /// <summary>Converts points to twips.</summary>
    public static int PointToTwip(double points)
        => (int)Math.Round(points * TwipsPerPoint);

    /// <summary>Converts twips to points.</summary>
    public static double TwipToPoint(double twips)
        => twips / TwipsPerPoint;
}
