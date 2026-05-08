namespace Tempo.Blazor.Components.Inputs;

/// <summary>Export formats produced by <see cref="TmSignatureCapture"/>.</summary>
public enum TmSignatureCaptureExportFormat
{
    /// <summary>Export as inline SVG markup.</summary>
    Svg,

    /// <summary>Export as a PNG data URL using JavaScript interop.</summary>
    PngDataUrl
}
