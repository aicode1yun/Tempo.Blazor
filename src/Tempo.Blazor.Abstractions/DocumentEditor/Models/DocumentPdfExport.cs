namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Request passed to a host PDF export provider.</summary>
public sealed class DocumentPdfExportRequest
{
    /// <summary>Document identifier.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Document snapshot to export.</summary>
    public DocumentEditorDocument Document { get; set; } = DocumentEditorDocument.Empty();

    /// <summary>Suggested file name without extension.</summary>
    public string? FileName { get; set; }

    /// <summary>Author requesting the export.</summary>
    public DocumentEditorAuthor? Author { get; set; }
}

/// <summary>PDF export provider result.</summary>
public sealed class DocumentPdfExportResult
{
    /// <summary>Exported PDF bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>PDF content type.</summary>
    public string ContentType { get; set; } = "application/pdf";

    /// <summary>Suggested file name.</summary>
    public string FileName { get; set; } = "document.pdf";
}
