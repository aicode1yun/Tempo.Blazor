using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.DocumentFormats.Pdf;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Server-side demo implementation of the document PDF export provider boundary. Every export
/// flows through the production WYSIWYG renderer: requests carrying the editor's canvas layout
/// snapshot render it directly, and snapshot-less requests (headless API clients, GET exports)
/// are laid out server-side via <see cref="ITempoDocumentLayoutService"/> — the same JS layout
/// chain the editor paints with, measured from the same font bytes the PDF embeds.
/// </summary>
public sealed class DemoDocumentPdfExportProvider : IDocumentPdfExportProvider
{
    private readonly ITempoDocumentLayoutService _layoutService;
    private readonly DemoDocumentExportFontCatalog _fontCatalog;
    private readonly TempoDocumentPdfRenderer _renderer;

    /// <summary>Creates the provider over the headless layout service and the demo font catalog.</summary>
    public DemoDocumentPdfExportProvider(
        ITempoDocumentLayoutService layoutService,
        DemoDocumentExportFontCatalog fontCatalog)
    {
        _layoutService = layoutService;
        _fontCatalog = fontCatalog;
        // The renderer embeds the same faces the headless layout measures with — measurement and
        // drawing agree by construction.
        _renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions
        {
            Fonts = fontCatalog.Fonts,
        });
    }

    /// <inheritdoc />
    public Task<DocumentPdfExportResult> ExportPdfAsync(
        DocumentPdfExportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = EnsurePdfFileName(request.FileName, request.DocumentId);
        if (string.IsNullOrWhiteSpace(request.LayoutSnapshotJson))
        {
            // Headless path: lay the document out server-side with the canvas layout chain.
            var options = request.Options ?? new DocumentPdfExportOptions();
            request.LayoutSnapshotJson = _layoutService.GenerateLayoutSnapshotJson(
                request.Document,
                options.PageSetup,
                _fontCatalog.Fonts,
                options.ReviewDisplayMode);
        }

        return Task.FromResult(new DocumentPdfExportResult
        {
            Content = _renderer.Render(request),
            ContentType = "application/pdf",
            FileName = fileName
        });
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
