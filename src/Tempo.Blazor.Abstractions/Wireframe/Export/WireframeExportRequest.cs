namespace Tempo.Blazor.Abstractions.Wireframe.Export;

/// <summary>
/// Request body sent from the wireframe editor to the export API.
/// </summary>
public sealed class WireframeExportRequest
{
    /// <summary>SVG markup string (already cleaned of UI overlays).</summary>
    public string Svg { get; set; } = "";

    /// <summary>Desired output file name without extension.</summary>
    public string FileName { get; set; } = "wireframe";

    /// <summary>Export options (scale, background, etc.).</summary>
    public WireframeExportOptions Options { get; set; } = new();
}
