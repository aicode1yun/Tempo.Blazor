namespace Tempo.Blazor.Reporting.Models;

/// <summary>Report metadata needed before rendering.</summary>
public sealed record ReportViewerMetadata
{
    /// <summary>Stable report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Report title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Viewer parameter metadata.</summary>
    public IReadOnlyList<ReportViewerParameterMetadata> Parameters { get; init; } = [];
}
