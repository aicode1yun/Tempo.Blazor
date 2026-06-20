namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Deterministic pixel metrics read directly from a canvas backing store.</summary>
public sealed class CanvasPixelMetrics
{
    /// <summary>Canvas backing-store width in device pixels.</summary>
    public int Width { get; set; }

    /// <summary>Canvas backing-store height in device pixels.</summary>
    public int Height { get; set; }

    /// <summary>Total pixels inspected.</summary>
    public int PixelCount { get; set; }

    /// <summary>Pixels whose alpha channel is non-zero.</summary>
    public int NonTransparentPixels { get; set; }

    /// <summary>Number of distinct sampled RGBA colors.</summary>
    public int DistinctColorCount { get; set; }

    /// <summary>Smallest X coordinate with a non-transparent pixel.</summary>
    public int? MinX { get; set; }

    /// <summary>Smallest Y coordinate with a non-transparent pixel.</summary>
    public int? MinY { get; set; }

    /// <summary>Largest X coordinate with a non-transparent pixel.</summary>
    public int? MaxX { get; set; }

    /// <summary>Largest Y coordinate with a non-transparent pixel.</summary>
    public int? MaxY { get; set; }

    /// <summary>Ratio of non-transparent pixels to inspected pixels.</summary>
    public double NonTransparentRatio => PixelCount == 0 ? 0 : (double)NonTransparentPixels / PixelCount;
}

/// <summary>Pixel diff metrics between two equally-sized canvas backing stores.</summary>
public sealed class CanvasPixelDelta
{
    /// <summary>Total comparable pixels.</summary>
    public int PixelCount { get; set; }

    /// <summary>Pixels whose RGBA values changed.</summary>
    public int ChangedPixels { get; set; }

    /// <summary>Smallest X coordinate with a changed pixel.</summary>
    public int? MinX { get; set; }

    /// <summary>Smallest Y coordinate with a changed pixel.</summary>
    public int? MinY { get; set; }

    /// <summary>Largest X coordinate with a changed pixel.</summary>
    public int? MaxX { get; set; }

    /// <summary>Largest Y coordinate with a changed pixel.</summary>
    public int? MaxY { get; set; }

    /// <summary>Ratio of changed pixels to comparable pixels.</summary>
    public double ChangedRatio => PixelCount == 0 ? 0 : (double)ChangedPixels / PixelCount;
}
