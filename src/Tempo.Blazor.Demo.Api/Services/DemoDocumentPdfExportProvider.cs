using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.DocumentFormats.Pdf;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Server-side demo implementation of the document PDF export provider boundary. Every export
/// flows through the production WYSIWYG renderer: requests carrying the editor's canvas layout
/// snapshot render it directly, and snapshot-less requests (headless API clients, GET exports)
/// are laid out server-side via <see cref="ITempoDocumentService"/> — the same JS layout chain
/// the editor paints with, measured from the same font bytes the PDF embeds, with asset-backed
/// image sources resolved from the demo image store so headless exports embed real images.
/// </summary>
public sealed class DemoDocumentPdfExportProvider : IDocumentPdfExportProvider
{
    private readonly ITempoDocumentService _documentService;
    private readonly DemoDocumentExportFontCatalog _fontCatalog;
    private readonly DemoDocumentEditorStore _store;
    private readonly TempoDocumentPdfRenderer _renderer;

    /// <summary>Creates the provider over the headless document facade, demo fonts and the demo image store.</summary>
    public DemoDocumentPdfExportProvider(
        ITempoDocumentService documentService,
        DemoDocumentExportFontCatalog fontCatalog,
        DemoDocumentEditorStore store)
    {
        _documentService = documentService;
        _fontCatalog = fontCatalog;
        _store = store;
        // The renderer embeds the same faces the headless layout measures with — measurement and
        // drawing agree by construction.
        _renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions
        {
            Fonts = fontCatalog.Fonts,
        });
    }

    /// <inheritdoc />
    public async Task<DocumentPdfExportResult> ExportPdfAsync(
        DocumentPdfExportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = EnsurePdfFileName(request.FileName, request.DocumentId);
        byte[] content;
        if (string.IsNullOrWhiteSpace(request.LayoutSnapshotJson))
        {
            // Headless path: lay the document out server-side with the canvas layout chain,
            // resolving asset-backed images from the demo store into embeddable data URIs.
            var rendered = await _documentService.RenderPdfAsync(new TempoDocumentRenderRequest
            {
                Document = request.Document,
                Options = request.Options,
                Fonts = _fontCatalog.Fonts,
                DocumentId = request.DocumentId,
                FileName = request.FileName,
                ImageResolver = ResolveImageSourceAsync,
            }, cancellationToken);
            content = rendered.PdfContent;
        }
        else
        {
            content = _renderer.Render(request);
        }

        return new DocumentPdfExportResult
        {
            Content = content,
            ContentType = "application/pdf",
            FileName = fileName
        };
    }

    /// <summary>
    /// Resolves demo image references: asset-backed sources come from the demo image store as
    /// data URIs; host-relative URLs are not resolvable server-side and keep their placeholder.
    /// </summary>
    private Task<string?> ResolveImageSourceAsync(TempoDocumentImageReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(reference.AssetId)
            && _store.GetImage(reference.AssetId!) is { } image)
        {
            return Task.FromResult<string?>($"data:{image.ContentType};base64,{Convert.ToBase64String(image.Content)}");
        }

        return Task.FromResult<string?>(null);
    }

    private static string EnsurePdfFileName(string? requestedName, string documentId)
    {
        var name = string.IsNullOrWhiteSpace(requestedName) ? documentId : requestedName;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name}.pdf";
    }
}
