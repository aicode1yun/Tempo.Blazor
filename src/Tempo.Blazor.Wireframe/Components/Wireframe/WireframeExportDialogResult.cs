using Tempo.Blazor.Abstractions.Wireframe.Export;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Result emitted by <see cref="TmWireframeExportDialog"/> when the user confirms export.
/// </summary>
public sealed class WireframeExportDialogResult
{
    /// <summary>Desired file name without extension.</summary>
    public string FileName { get; set; } = "wireframe";

    /// <summary>"png" or "pdf".</summary>
    public string Format { get; set; } = "png";

    /// <summary>Export options (scale, background, etc.).</summary>
    public WireframeExportOptions Options { get; set; } = new();
}
