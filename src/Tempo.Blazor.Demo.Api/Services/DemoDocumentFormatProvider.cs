using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;

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
        if (request.Format != DocumentFormatProviderKind.Docx)
        {
            return new DocumentFormatImportProviderResult
            {
                Success = false,
                Format = request.Format,
                ErrorMessage = "Unsupported document format."
            };
        }

        await using var stream = new MemoryStream(request.Content);
        var imported = await new DocumentDocxImporter().ImportAsync(stream, new DocumentFormatImportOptions
        {
            DocumentId = request.DocumentId,
            FileName = request.FileName,
            ImageImporter = ImportImageAsync
        }, cancellationToken);

        return new DocumentFormatImportProviderResult
        {
            Success = true,
            Document = imported.Document,
            Format = DocumentFormatProviderKind.Docx,
            Warnings = MapWarnings(imported.Warnings, request.FileName)
        };
    }

    /// <inheritdoc />
    public async Task<DocumentFormatExportProviderResult> ExportAsync(
        DocumentFormatExportProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Format != DocumentFormatProviderKind.Docx)
        {
            return new DocumentFormatExportProviderResult
            {
                Success = false,
                Format = request.Format,
                ErrorMessage = "Unsupported document format."
            };
        }

        var exported = await new DocumentDocxExporter().ExportAsync(request.Document, new DocumentFormatExportOptions
        {
            FileName = request.FileName,
            ImageResolver = ResolveImageAsync
        }, cancellationToken);

        return new DocumentFormatExportProviderResult
        {
            Success = true,
            Content = exported.Content,
            ContentType = exported.ContentType,
            FileName = exported.FileName,
            Format = DocumentFormatProviderKind.Docx,
            Warnings = MapWarnings(exported.Warnings, request.FileName)
        };
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
