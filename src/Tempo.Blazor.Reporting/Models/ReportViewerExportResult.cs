namespace Tempo.Blazor.Reporting.Models;

/// <summary>Binary export returned by a report source.</summary>
public sealed record ReportViewerExportResult
{
    /// <summary>Creates an empty export result.</summary>
    public ReportViewerExportResult()
    {
    }

    /// <summary>Creates an export result.</summary>
    public ReportViewerExportResult(byte[] bytes, string fileName, string contentType)
    {
        Bytes = bytes;
        FileName = fileName;
        ContentType = contentType;
    }

    /// <summary>Export bytes.</summary>
    public byte[] Bytes { get; init; } = [];

    /// <summary>Suggested file name.</summary>
    public string FileName { get; init; } = "report.pdf";

    /// <summary>MIME content type.</summary>
    public string ContentType { get; init; } = "application/pdf";
}
