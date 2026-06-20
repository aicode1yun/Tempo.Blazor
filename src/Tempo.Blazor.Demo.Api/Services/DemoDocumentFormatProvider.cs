using System.Text;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Html;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo implementation of the document format provider boundary.</summary>
public sealed class DemoDocumentFormatProvider : IDocumentFormatProvider
{
    private readonly DemoDocumentEditorStore _store;

    private static readonly IReadOnlyList<DocumentFormatProviderCapability> Capabilities =
    [
        new()
        {
            Format = DocumentFormatProviderKind.Docx,
            CanImport = true,
            CanExport = true,
            FileExtensions = [".docx"],
            ContentTypes = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]
        },
        new()
        {
            Format = DocumentFormatProviderKind.Odt,
            CanImport = true,
            CanExport = true,
            FileExtensions = [".odt"],
            ContentTypes = ["application/vnd.oasis.opendocument.text"]
        },
        new()
        {
            Format = DocumentFormatProviderKind.Html,
            CanImport = true,
            CanExport = true,
            FileExtensions = [".html", ".htm"],
            ContentTypes = ["text/html"]
        },
        new()
        {
            Format = DocumentFormatProviderKind.Markdown,
            CanImport = true,
            CanExport = true,
            FileExtensions = [".md", ".markdown"],
            ContentTypes = ["text/markdown", "text/x-markdown", "text/plain"]
        }
    ];

    /// <summary>Creates the demo document format provider.</summary>
    public DemoDocumentFormatProvider(DemoDocumentEditorStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Capabilities);
    }

    /// <inheritdoc />
    public async Task<DocumentFormatImportProviderResult> ImportAsync(
        DocumentFormatImportProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Capabilities.Any(capability => capability.Format == request.Format && capability.CanImport))
        {
            return new DocumentFormatImportProviderResult
            {
                Success = false,
                Format = request.Format,
                ErrorMessage = "Unsupported document format."
            };
        }

        var imported = await ImportDocumentAsync(request, cancellationToken);

        return new DocumentFormatImportProviderResult
        {
            Success = true,
            Document = imported.Document,
            Format = request.Format,
            Warnings = MapWarnings(imported.Warnings, request.FileName)
        };
    }

    private async Task<DocumentFormatImportResult> ImportDocumentAsync(
        DocumentFormatImportProviderRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(request.Content);
        var options = new DocumentFormatImportOptions
        {
            DocumentId = request.DocumentId,
            FileName = request.FileName,
            ImageImporter = ImportImageAsync
        };

        return request.Format switch
        {
            DocumentFormatProviderKind.Docx => await new DocumentDocxImporter().ImportAsync(stream, options, cancellationToken),
            DocumentFormatProviderKind.Odt => await new DocumentOdtImporter().ImportAsync(stream, options, cancellationToken),
            DocumentFormatProviderKind.Html => new DocumentFormatImportResult
            {
                Document = new DocumentHtmlImporter().Import(Encoding.UTF8.GetString(request.Content), new DocumentHtmlImportOptions
                {
                    DocumentId = request.DocumentId,
                    Title = Path.GetFileNameWithoutExtension(request.FileName)
                }),
                Format = DocumentFormatKind.Html
            },
            DocumentFormatProviderKind.Markdown => await new DocumentMarkdownImporter().ImportAsync(stream, options, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported document format.")
        };
    }

    /// <inheritdoc />
    public async Task<DocumentFormatExportProviderResult> ExportAsync(
        DocumentFormatExportProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Capabilities.Any(capability => capability.Format == request.Format && capability.CanExport))
        {
            return new DocumentFormatExportProviderResult
            {
                Success = false,
                Format = request.Format,
                ErrorMessage = "Unsupported document format."
            };
        }

        var exported = await ExportDocumentAsync(request, cancellationToken);

        return new DocumentFormatExportProviderResult
        {
            Success = true,
            Content = exported.Content,
            ContentType = exported.ContentType,
            FileName = exported.FileName,
            Format = request.Format,
            Warnings = MapWarnings(exported.Warnings, request.FileName)
        };
    }

    private async Task<DocumentFormatExportResult> ExportDocumentAsync(
        DocumentFormatExportProviderRequest request,
        CancellationToken cancellationToken)
    {
        var options = new DocumentFormatExportOptions
        {
            FileName = request.FileName,
            ImageResolver = ResolveImageAsync
        };

        return request.Format switch
        {
            DocumentFormatProviderKind.Docx => await new DocumentDocxExporter().ExportAsync(request.Document, options, cancellationToken),
            DocumentFormatProviderKind.Odt => await new DocumentOdtExporter().ExportAsync(request.Document, options, cancellationToken),
            DocumentFormatProviderKind.Html => ExportHtml(request),
            DocumentFormatProviderKind.Markdown => ExportMarkdown(request),
            _ => throw new InvalidOperationException("Unsupported document format.")
        };
    }

    private static DocumentFormatExportResult ExportHtml(DocumentFormatExportProviderRequest request)
    {
        var html = new DocumentHtmlExporter().Export(request.Document, new DocumentHtmlExportOptions
        {
            IncludeDocumentWrapper = true
        });

        return new DocumentFormatExportResult
        {
            Content = Encoding.UTF8.GetBytes(html),
            ContentType = "text/html; charset=utf-8",
            FileName = EnsureExtension(request.FileName, request.Document, ".html"),
            Format = DocumentFormatKind.Html
        };
    }

    private static DocumentFormatExportResult ExportMarkdown(DocumentFormatExportProviderRequest request)
    {
        var markdown = new DocumentMarkdownExporter().Export(request.Document);
        return new DocumentFormatExportResult
        {
            Content = Encoding.UTF8.GetBytes(markdown),
            ContentType = "text/markdown; charset=utf-8",
            FileName = EnsureExtension(request.FileName, request.Document, ".md"),
            Format = DocumentFormatKind.Markdown
        };
    }

    private static string EnsureExtension(string? requestedFileName, DocumentEditorDocument document, string extension)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedFileName)
            ? string.IsNullOrWhiteSpace(document.Metadata.Title) ? document.DocumentId : document.Metadata.Title
            : requestedFileName;
        var sanitized = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        sanitized = string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
        return sanitized.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? sanitized
            : sanitized + extension;
    }

    private async Task<DocumentFormatImageImportResult> ImportImageAsync(
        DocumentFormatImageImportRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(request.Content);
        var asset = await _store.SaveImageAsync(
            string.IsNullOrWhiteSpace(request.FileName) ? "imported-docx-image.png" : request.FileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "image/png" : request.ContentType,
            stream,
            cancellationToken);

        return new DocumentFormatImageImportResult
        {
            AssetId = asset.Id
        };
    }

    private Task<DocumentFormatImageExportResult?> ResolveImageAsync(
        DocumentFormatImageExportRequest request,
        CancellationToken cancellationToken)
    {
        var image = _store.GetImage(request.AssetId);
        return Task.FromResult(image is null
            ? null
            : new DocumentFormatImageExportResult
            {
                Content = image.Content,
                ContentType = image.ContentType,
                FileName = image.FileName
            });
    }

    private static List<DocumentFormatProviderWarning> MapWarnings(
        IReadOnlyList<DocumentFormatCompatibilityWarning> warnings,
        string? fileName)
    {
        var mapped = warnings
            .Select(warning => new DocumentFormatProviderWarning
            {
                Code = warning.Code,
                Message = warning.Message,
                SourcePath = warning.SourcePath,
                ObjectId = warning.ObjectId,
                Severity = warning.Severity switch
                {
                    DocumentFormatCompatibilitySeverity.Info => DocumentFormatProviderWarningSeverity.Info,
                    DocumentFormatCompatibilitySeverity.Dropped => DocumentFormatProviderWarningSeverity.Dropped,
                    _ => DocumentFormatProviderWarningSeverity.Warning
                }
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(fileName)
            && fileName.Contains("warning", StringComparison.OrdinalIgnoreCase)
            && mapped.All(warning => warning.Code != "demo.approximation"))
        {
            mapped.Add(new DocumentFormatProviderWarning
            {
                Code = "demo.approximation",
                Message = "DOCX was imported with demo approximation warnings.",
                Severity = DocumentFormatProviderWarningSeverity.Warning
            });
        }

        return mapped;
    }
}
