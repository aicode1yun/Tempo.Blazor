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

    /// <summary>Options that control PDF generation.</summary>
    public DocumentPdfExportOptions Options { get; set; } = new();
}

/// <summary>Options that control PDF export behavior.</summary>
public sealed class DocumentPdfExportOptions
{
    /// <summary>Whether provider-backed suggestions should be included in the exported PDF.</summary>
    public bool IncludeSuggestions { get; set; } = true;

    /// <summary>Whether comments should be included in the exported PDF.</summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>Page setup used by the PDF renderer.</summary>
    public DocumentPdfPageSetupOptions PageSetup { get; set; } = new();
}

/// <summary>Page setup options for PDF export.</summary>
public sealed class DocumentPdfPageSetupOptions
{
    /// <summary>Page size in points.</summary>
    public DocumentPageSize PageSize { get; set; } = DocumentPageSize.A4;

    /// <summary>Page orientation.</summary>
    public DocumentPdfPageOrientation Orientation { get; set; } = DocumentPdfPageOrientation.Portrait;

    /// <summary>Page margins in points.</summary>
    public DocumentPageMargins Margins { get; set; } = DocumentPageMargins.Default;
}

/// <summary>Page orientation for PDF export.</summary>
public enum DocumentPdfPageOrientation
{
    /// <summary>Portrait page orientation.</summary>
    Portrait,

    /// <summary>Landscape page orientation.</summary>
    Landscape
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
