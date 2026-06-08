using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed document format provider used by the document editor demo.</summary>
public sealed class DemoDocumentFormatProvider : IDocumentFormatProvider
{
    private readonly HttpClient? _http;

    /// <summary>Creates the provider and optionally binds it to the demo API client.</summary>
    public DemoDocumentFormatProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentFormatProviderCapability> capabilities =
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

        return Task.FromResult(capabilities);
    }

    /// <inheritdoc />
    public async Task<DocumentFormatImportProviderResult> ImportAsync(
        DocumentFormatImportProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is null)
        {
            return FailedImport(request.Format, "Demo API is not available.");
        }

        try
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(request.Content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType);
            form.Add(fileContent, "file", request.FileName);

            using var response = await _http.PostAsync(
                $"api/document-editor/formats/import?format={request.Format}",
                form,
                cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<DocumentFormatImportProviderResult>(
                cancellationToken);

            if (response.IsSuccessStatusCode && result is not null)
            {
                return result;
            }

            return result ?? FailedImport(request.Format, "Document import failed.");
        }
        catch
        {
            return FailedImport(request.Format, "Document import failed.");
        }
    }

    /// <inheritdoc />
    public async Task<DocumentFormatExportProviderResult> ExportAsync(
        DocumentFormatExportProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is null)
        {
            return FailedExport(request.Format, "Demo API is not available.");
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/document-editor/formats/export",
                request,
                cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<DocumentFormatExportProviderResult>(
                cancellationToken);

            if (response.IsSuccessStatusCode && result is not null)
            {
                return result;
            }

            return result ?? FailedExport(request.Format, "Document export failed.");
        }
        catch
        {
            return FailedExport(request.Format, "Document export failed.");
        }
    }

    private static DocumentFormatImportProviderResult FailedImport(DocumentFormatProviderKind format, string message)
    {
        return new DocumentFormatImportProviderResult
        {
            Success = false,
            Format = format,
            ErrorMessage = message
        };
    }

    private static DocumentFormatExportProviderResult FailedExport(DocumentFormatProviderKind format, string message)
    {
        return new DocumentFormatExportProviderResult
        {
            Success = false,
            Format = format,
            ErrorMessage = message
        };
    }
}
