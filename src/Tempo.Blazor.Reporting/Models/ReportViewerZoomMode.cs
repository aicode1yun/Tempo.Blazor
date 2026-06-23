namespace Tempo.Blazor.Reporting.Models;

/// <summary>Report viewer zoom behavior.</summary>
public enum ReportViewerZoomMode
{
    /// <summary>Use an explicit percentage.</summary>
    Percent,

    /// <summary>Scale each page to fit the viewport width.</summary>
    FitWidth,

    /// <summary>Scale each page so the entire page is visible.</summary>
    FitPage,
}
