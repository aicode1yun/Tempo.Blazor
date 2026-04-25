namespace Tempo.Blazor.Abstractions.Wireframe.Export;

/// <summary>
/// Options that control wireframe export output (PNG / PDF).
/// </summary>
public sealed class WireframeExportOptions
{
    /// <summary>Include page background colour in the output. Default true.</summary>
    public bool IncludeBackground { get; set; } = true;

    /// <summary>
    /// Raster scale factor for PNG exports (1 = 1×, 2 = 2× retina, 3 = 3×).
    /// Ignored for PDF.
    /// </summary>
    public int Scale { get; set; } = 1;

    /// <summary>
    /// For PDF exports: "all" or comma-separated page indices (0-based).
    /// Ignored for PNG.
    /// </summary>
    public string PageRange { get; set; } = "all";

    /// <summary>Background colour hex string (e.g. #ffffff). Empty = transparent.</summary>
    public string? BackgroundColor { get; set; }
}
