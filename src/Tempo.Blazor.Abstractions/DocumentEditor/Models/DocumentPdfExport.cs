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

    /// <summary>
    /// Optional canvas layout snapshot (schema v1) captured from the editor at export time:
    /// a page-indexed list of print primitives (text / rect / line / image / path) produced by the
    /// canvas engine's <c>getLayoutSnapshotJson</c> interop. When present, WYSIWYG-parity renderers
    /// reuse the editor's exact line and page breaking instead of re-laying the document out.
    /// </summary>
    public string? LayoutSnapshotJson { get; set; }
}

/// <summary>Options that control PDF export behavior.</summary>
public sealed class DocumentPdfExportOptions
{
    /// <summary>Whether provider-backed suggestions should be included in the exported PDF.</summary>
    public bool IncludeSuggestions { get; set; } = true;

    /// <summary>Whether comments should be included in the exported PDF.</summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>Tracked changes display mode requested for the exported PDF.</summary>
    public DocumentReviewDisplayMode ReviewDisplayMode { get; set; } = DocumentReviewDisplayMode.AllMarkup;

    /// <summary>Page setup used by the PDF renderer.</summary>
    public DocumentPdfPageSetupOptions PageSetup { get; set; } = new();

    /// <summary>
    /// Optional forensic watermark stamped on every exported page (reader name, export time, IP).
    /// Null (the default) exports without a forensic stamp; hosts should fill server-known fields
    /// (IP address, timestamp) server-side so the stamp cannot be spoofed by the client.
    /// </summary>
    public DocumentPdfForensicWatermarkOptions? ForensicWatermark { get; set; }
}

/// <summary>Forensic watermark options for PDF export: identifies who exported the document and when.</summary>
public sealed class DocumentPdfForensicWatermarkOptions
{
    /// <summary>Display name of the exporting user.</summary>
    public string? UserName { get; set; }

    /// <summary>Client IP address (fill server-side).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Export timestamp; null lets the renderer stamp the current UTC time.</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>Stamp opacity from 0 to 1.</summary>
    public double Opacity { get; set; } = 0.14;

    /// <summary>Stamp rotation in degrees.</summary>
    public double Rotation { get; set; } = -36;
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
