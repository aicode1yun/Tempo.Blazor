namespace Tempo.Blazor.Reporting.Models;

/// <summary>Zoom state passed from the Blazor viewer to the canvas module.</summary>
public sealed record ReportViewerZoom
{
    /// <summary>Creates the default 100% zoom.</summary>
    public ReportViewerZoom()
    {
    }

    /// <summary>Creates a zoom state.</summary>
    public ReportViewerZoom(ReportViewerZoomMode mode, int percent)
    {
        Mode = mode;
        Percent = Math.Clamp(percent, 25, 400);
    }

    /// <summary>Zoom mode.</summary>
    public ReportViewerZoomMode Mode { get; init; } = ReportViewerZoomMode.Percent;

    /// <summary>Explicit zoom percentage used when <see cref="Mode"/> is <see cref="ReportViewerZoomMode.Percent"/>.</summary>
    public int Percent { get; init; } = 100;
}
