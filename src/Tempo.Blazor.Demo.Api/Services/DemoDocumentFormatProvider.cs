using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo implementation of the document format provider boundary.</summary>
public sealed class DemoDocumentFormatProvider : IDocumentFormatProvider
{
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
            FileName = request.FileName
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
            FileName = request.FileName
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
