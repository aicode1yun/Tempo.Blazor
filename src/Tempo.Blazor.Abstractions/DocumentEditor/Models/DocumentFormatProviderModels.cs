namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>External document format supported through a host provider boundary.</summary>
public enum DocumentFormatProviderKind
{
    /// <summary>Microsoft Word Open XML document package.</summary>
    Docx,

    /// <summary>OpenDocument Text package.</summary>
    Odt,

    /// <summary>Semantic HTML document.</summary>
    Html,

    /// <summary>Markdown document.</summary>
    Markdown
}

/// <summary>Severity of an import/export compatibility warning.</summary>
public enum DocumentFormatProviderWarningSeverity
{
    /// <summary>The feature was normalized into a supported model shape.</summary>
    Info,

    /// <summary>The feature was imported or exported with a known approximation.</summary>
    Warning,

    /// <summary>The feature could not be represented and was dropped.</summary>
    Dropped
}

/// <summary>Import/export capability exposed by an external document format provider.</summary>
public sealed class DocumentFormatProviderCapability
{
    /// <summary>External format.</summary>
    public DocumentFormatProviderKind Format { get; set; } = DocumentFormatProviderKind.Docx;

    /// <summary>Whether the provider can import the format.</summary>
    public bool CanImport { get; set; }

    /// <summary>Whether the provider can export the format.</summary>
    public bool CanExport { get; set; }

    /// <summary>Accepted file extensions including the leading dot.</summary>
    public List<string> FileExtensions { get; set; } = [];

    /// <summary>Accepted content types.</summary>
    public List<string> ContentTypes { get; set; } = [];
}

/// <summary>Compatibility warning returned from a provider-backed import or export operation.</summary>
public sealed class DocumentFormatProviderWarning
{
    /// <summary>Stable warning code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable warning text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Warning severity.</summary>
    public DocumentFormatProviderWarningSeverity Severity { get; set; } = DocumentFormatProviderWarningSeverity.Warning;

    /// <summary>Optional source path inside the package or provider.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Optional editor drawing object id related to the warning.</summary>
    public string? ObjectId { get; set; }
}

/// <summary>Request passed to a provider-backed document import operation.</summary>
public sealed class DocumentFormatImportProviderRequest
{
    /// <summary>Target document id for the imported editor model.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>External source format.</summary>
    public DocumentFormatProviderKind Format { get; set; } = DocumentFormatProviderKind.Docx;

    /// <summary>Uploaded file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Uploaded content type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Uploaded package bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Actor who requested the import.</summary>
    public DocumentEditorAuthor? Author { get; set; }
}

/// <summary>Result returned from a provider-backed document import operation.</summary>
public sealed class DocumentFormatImportProviderResult
{
    /// <summary>Whether the import completed successfully.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Imported editor document.</summary>
    public DocumentEditorDocument? Document { get; set; }

    /// <summary>Imported source format.</summary>
    public DocumentFormatProviderKind Format { get; set; } = DocumentFormatProviderKind.Docx;

    /// <summary>Compatibility warnings emitted during import.</summary>
    public List<DocumentFormatProviderWarning> Warnings { get; set; } = [];

    /// <summary>Optional provider error message.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Request passed to a provider-backed document export operation.</summary>
public sealed class DocumentFormatExportProviderRequest
{
    /// <summary>Current document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Target external format.</summary>
    public DocumentFormatProviderKind Format { get; set; } = DocumentFormatProviderKind.Docx;

    /// <summary>Document snapshot to export.</summary>
    public DocumentEditorDocument Document { get; set; } = DocumentEditorDocument.Empty();

    /// <summary>Suggested file name without path.</summary>
    public string? FileName { get; set; }

    /// <summary>Actor who requested the export.</summary>
    public DocumentEditorAuthor? Author { get; set; }
}

/// <summary>Result returned from a provider-backed document export operation.</summary>
public sealed class DocumentFormatExportProviderResult
{
    /// <summary>Whether the export completed successfully.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Exported package bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Content type of the exported package.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Suggested download file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Exported target format.</summary>
    public DocumentFormatProviderKind Format { get; set; } = DocumentFormatProviderKind.Docx;

    /// <summary>Compatibility warnings emitted during export.</summary>
    public List<DocumentFormatProviderWarning> Warnings { get; set; } = [];

    /// <summary>Optional provider error message.</summary>
    public string? ErrorMessage { get; set; }
}
