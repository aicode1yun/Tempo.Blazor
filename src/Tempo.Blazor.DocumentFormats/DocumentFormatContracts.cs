using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats;

/// <summary>Supported external document format.</summary>
public enum DocumentFormatKind
{
    /// <summary>Microsoft Word Open XML document package.</summary>
    Docx,

    /// <summary>OpenDocument Text package.</summary>
    Odt
}

/// <summary>Severity of an import/export compatibility message.</summary>
public enum DocumentFormatCompatibilitySeverity
{
    /// <summary>The feature was normalized into a supported model shape.</summary>
    Info,

    /// <summary>The feature was imported/exported with a known approximation.</summary>
    Warning,

    /// <summary>The feature could not be represented and was dropped.</summary>
    Dropped
}

/// <summary>Compatibility warning emitted by an importer or exporter.</summary>
public sealed class DocumentFormatCompatibilityWarning
{
    /// <summary>Warning code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Warning severity.</summary>
    public DocumentFormatCompatibilitySeverity Severity { get; set; } = DocumentFormatCompatibilitySeverity.Warning;

    /// <summary>Optional source path inside the package.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Optional editor drawing object id related to the warning.</summary>
    public string? ObjectId { get; set; }
}

/// <summary>Options used when importing an external package.</summary>
public sealed class DocumentFormatImportOptions
{
    /// <summary>Optional document id assigned to the imported model.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional file name used for metadata fallback.</summary>
    public string? FileName { get; set; }

    /// <summary>Optional callback that persists extracted image bytes and returns an asset id.</summary>
    public Func<DocumentFormatImageImportRequest, CancellationToken, Task<DocumentFormatImageImportResult>>? ImageImporter { get; set; }

    /// <summary>Maximum embedded image part size accepted during import, in bytes.</summary>
    public long MaxImagePartBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>Maximum raw DrawingML XML size preserved for unsupported drawing fallback metadata, in characters.</summary>
    public int MaxRawDrawingXmlChars { get; set; } = 128 * 1024;

    /// <summary>Whether the importer may download externally linked images. Defaults to false for untrusted packages.</summary>
    public bool AllowExternalImageDownload { get; set; }
}

/// <summary>Options used when exporting an external package.</summary>
public sealed class DocumentFormatExportOptions
{
    /// <summary>Optional file name without extension.</summary>
    public string? FileName { get; set; }

    /// <summary>Optional callback that resolves asset-backed image bytes.</summary>
    public Func<DocumentFormatImageExportRequest, CancellationToken, Task<DocumentFormatImageExportResult?>>? ImageResolver { get; set; }

    /// <summary>Whether unsupported or unavailable images may be exported as transparent placeholders.</summary>
    public bool AllowImagePlaceholders { get; set; }

    /// <summary>Maximum image payload size accepted during export, in bytes.</summary>
    public long MaxImagePartBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>Whether the exporter may download external image URLs. Defaults to false for untrusted content.</summary>
    public bool AllowExternalImageDownload { get; set; }
}

/// <summary>Result returned from an external package importer.</summary>
public sealed class DocumentFormatImportResult
{
    /// <summary>Imported document.</summary>
    public DocumentEditorDocument Document { get; set; } = DocumentEditorDocument.Empty();

    /// <summary>Source format.</summary>
    public DocumentFormatKind Format { get; set; }

    /// <summary>Compatibility warnings.</summary>
    public List<DocumentFormatCompatibilityWarning> Warnings { get; set; } = [];

    /// <summary>Preserved package parts that were not mapped into the editor model.</summary>
    public List<DocumentFormatPreservedPart> PreservedParts { get; set; } = [];
}

/// <summary>Result returned from an external package exporter.</summary>
public sealed class DocumentFormatExportResult
{
    /// <summary>Exported package bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Suggested file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Target format.</summary>
    public DocumentFormatKind Format { get; set; }

    /// <summary>Compatibility warnings.</summary>
    public List<DocumentFormatCompatibilityWarning> Warnings { get; set; } = [];
}

/// <summary>Summary of an import/export round-trip through the editor document model.</summary>
public sealed class DocumentPackageRoundTripReport
{
    /// <summary>Source package format.</summary>
    public DocumentFormatKind SourceFormat { get; set; }

    /// <summary>Target package format.</summary>
    public DocumentFormatKind TargetFormat { get; set; }

    /// <summary>Whether no warnings were emitted by either side of the round-trip.</summary>
    public bool IsLossless => Warnings.Count == 0;

    /// <summary>Combined compatibility warnings from import and export.</summary>
    public List<DocumentFormatCompatibilityWarning> Warnings { get; set; } = [];

    /// <summary>Package parts preserved by the importer but not mapped into the semantic document model.</summary>
    public List<DocumentFormatPreservedPart> PreservedParts { get; set; } = [];

    /// <summary>Creates a report from import and export results.</summary>
    public static DocumentPackageRoundTripReport Create(DocumentFormatImportResult importResult, DocumentFormatExportResult exportResult)
    {
        ArgumentNullException.ThrowIfNull(importResult);
        ArgumentNullException.ThrowIfNull(exportResult);

        return new DocumentPackageRoundTripReport
        {
            SourceFormat = importResult.Format,
            TargetFormat = exportResult.Format,
            Warnings = importResult.Warnings.Concat(exportResult.Warnings).ToList(),
            PreservedParts = importResult.PreservedParts.ToList()
        };
    }
}

/// <summary>Package part preserved outside the semantic document model.</summary>
public sealed class DocumentFormatPreservedPart
{
    /// <summary>Package path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Part content type when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Raw part bytes.</summary>
    public byte[] Content { get; set; } = [];
}

/// <summary>Image extracted during import.</summary>
public sealed class DocumentFormatImageImportRequest
{
    /// <summary>Package path or relationship id.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Image content type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Image bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Suggested file name.</summary>
    public string? FileName { get; set; }
}

/// <summary>Image import callback result.</summary>
public sealed class DocumentFormatImageImportResult
{
    /// <summary>Provider asset id.</summary>
    public string? AssetId { get; set; }

    /// <summary>Optional direct URL.</summary>
    public string? Url { get; set; }
}

/// <summary>Image requested during export.</summary>
public sealed class DocumentFormatImageExportRequest
{
    /// <summary>Asset id.</summary>
    public string AssetId { get; set; } = string.Empty;
}

/// <summary>Image bytes resolved during export.</summary>
public sealed class DocumentFormatImageExportResult
{
    /// <summary>Image content type.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>Image bytes.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Optional original file name used to infer image type when content type is not available.</summary>
    public string? FileName { get; set; }
}

/// <summary>External package importer.</summary>
public interface IDocumentFormatImporter
{
    /// <summary>Imports an external document stream.</summary>
    Task<DocumentFormatImportResult> ImportAsync(Stream stream, DocumentFormatImportOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>External package exporter.</summary>
public interface IDocumentFormatExporter
{
    /// <summary>Exports an editor document to an external package.</summary>
    Task<DocumentFormatExportResult> ExportAsync(DocumentEditorDocument document, DocumentFormatExportOptions? options = null, CancellationToken cancellationToken = default);
}
