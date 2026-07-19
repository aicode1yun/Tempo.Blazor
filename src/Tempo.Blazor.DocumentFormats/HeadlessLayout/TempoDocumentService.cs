using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Pdf;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>Request for the headless document facade: a template or plain document plus the data to render it with.</summary>
public sealed class TempoDocumentRenderRequest
{
    /// <summary>Template or plain document to render.</summary>
    public required DocumentEditorDocument Document { get; init; }

    /// <summary>
    /// Token values for document assembly (IF/ELSE chains, repeating sections, computed
    /// expressions). Null renders the document as-is without assembly.
    /// </summary>
    public IReadOnlyDictionary<string, DocumentTokenValue>? TokenValues { get; init; }

    /// <summary>Export options: page setup, review display mode, watermark and forensic watermark.</summary>
    public DocumentPdfExportOptions? Options { get; init; }

    /// <summary>Font faces to measure and embed with — required for WYSIWYG-accurate output.</summary>
    public IReadOnlyList<ReportPdfFontFace>? Fonts { get; init; }

    /// <summary>Document identifier used for the export request; defaults to the document's own id.</summary>
    public string? DocumentId { get; init; }

    /// <summary>Suggested file name without extension.</summary>
    public string? FileName { get; init; }
}

/// <summary>Result of a headless PDF render.</summary>
public sealed class TempoDocumentPdfResult
{
    /// <summary>PDF bytes.</summary>
    public required byte[] PdfContent { get; init; }

    /// <summary>Number of laid-out pages.</summary>
    public required int PageCount { get; init; }

    /// <summary>The layout snapshot JSON (schema v1) the PDF was rendered from.</summary>
    public required string LayoutSnapshotJson { get; init; }

    /// <summary>Forensic watermark timestamp stamped into the PDF, when a forensic watermark was requested.</summary>
    public DateTimeOffset? ForensicTimestamp { get; init; }
}

/// <summary>One rendered page preview.</summary>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="Width">Raster width in pixels.</param>
/// <param name="Height">Raster height in pixels.</param>
/// <param name="Png">PNG bytes.</param>
public sealed record TempoDocumentPageImage(int PageIndex, int Width, int Height, byte[] Png);

/// <summary>
/// Headless document rendering: assembly (template + token values), layout, PDF and page
/// previews in one call. See <see cref="TempoDocumentService"/>.
/// </summary>
public interface ITempoDocumentService
{
    /// <summary>
    /// Renders a template or document to a WYSIWYG PDF: DocumentAssemblyService.Assemble (when
    /// token values are provided) → ITempoDocumentLayoutService (headless canvas layout) →
    /// TempoDocumentPdfRenderer (vector PDF with embedded fonts, watermarks, forensic stamp).
    /// </summary>
    Task<TempoDocumentPdfResult> RenderPdfAsync(TempoDocumentRenderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders per-page PNG previews of the laid-out document at the requested DPI (96 = CSS
    /// pixel scale) — visual feedback for agents, tests and preview tooling.
    /// </summary>
    Task<IReadOnlyList<TempoDocumentPageImage>> RenderPageImagesAsync(
        TempoDocumentRenderRequest request,
        double dpi = 96,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ITempoDocumentService"/>: composes the server-side document pipeline —
/// assembly, headless canvas layout (Jint) and Skia PDF/raster rendering. The clock is
/// injectable (<see cref="TimeProvider"/>), so assembly date functions (TODAY/DATEADD) and the
/// forensic watermark timestamp are deterministic under test.
/// </summary>
public sealed class TempoDocumentService : ITempoDocumentService
{
    private const double CssDpi = 96;

    private readonly ITempoDocumentLayoutService _layoutService;
    private readonly TimeProvider _timeProvider;
    private readonly DocumentAssemblyService _assemblyService = new();
    private readonly ReportPdfRenderer _rasterRenderer = new();

    /// <summary>Creates the facade over the headless layout service and an optional clock.</summary>
    public TempoDocumentService(ITempoDocumentLayoutService layoutService, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(layoutService);
        _layoutService = layoutService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<TempoDocumentPdfResult> RenderPdfAsync(
        TempoDocumentRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var (exportRequest, fonts, forensicTimestamp) = PrepareExportRequest(request);
        var renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions { Fonts = fonts });
        return Task.FromResult(new TempoDocumentPdfResult
        {
            PdfContent = renderer.Render(exportRequest),
            PageCount = PageCountOf(exportRequest.LayoutSnapshotJson!),
            LayoutSnapshotJson = exportRequest.LayoutSnapshotJson!,
            ForensicTimestamp = forensicTimestamp,
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TempoDocumentPageImage>> RenderPageImagesAsync(
        TempoDocumentRenderRequest request,
        double dpi = 96,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!double.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be a positive finite number.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var (exportRequest, fonts, _) = PrepareExportRequest(request);
        var renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions { Fonts = fonts });
        var snapshot = renderer.BuildReportSnapshot(exportRequest);
        var rasterOptions = new ReportPdfRendererOptions { Fonts = fonts };
        var scale = dpi / CssDpi;

        var images = new List<TempoDocumentPageImage>(snapshot.Pages.Count);
        foreach (var page in snapshot.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var png = _rasterRenderer.RenderPagePng(page, rasterOptions, scale);
            images.Add(new TempoDocumentPageImage(
                images.Count,
                Math.Max(1, (int)Math.Ceiling(page.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(page.Height * scale)),
                png));
        }

        return Task.FromResult<IReadOnlyList<TempoDocumentPageImage>>(images);
    }

    private (DocumentPdfExportRequest ExportRequest, IReadOnlyList<ReportPdfFontFace> Fonts, DateTimeOffset? ForensicTimestamp)
        PrepareExportRequest(TempoDocumentRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Document);
        var fonts = request.Fonts ?? [];

        var document = request.TokenValues is null
            ? request.Document
            : _assemblyService.Assemble(
                request.Document,
                request.TokenValues,
                new DocumentAssemblyOptions { Now = _timeProvider.GetUtcNow() });

        var options = request.Options ?? new DocumentPdfExportOptions();
        DateTimeOffset? forensicTimestamp = null;
        if (options.ForensicWatermark is { } forensic)
        {
            // Deterministic forensic stamp: a missing timestamp comes from the injected clock,
            // not from an uncontrolled DateTimeOffset.UtcNow deep inside the renderer. The
            // caller's options are cloned, never mutated.
            forensicTimestamp = forensic.Timestamp ?? _timeProvider.GetUtcNow();
            options = new DocumentPdfExportOptions
            {
                IncludeSuggestions = options.IncludeSuggestions,
                IncludeComments = options.IncludeComments,
                ReviewDisplayMode = options.ReviewDisplayMode,
                PageSetup = options.PageSetup,
                ForensicWatermark = new DocumentPdfForensicWatermarkOptions
                {
                    UserName = forensic.UserName,
                    IpAddress = forensic.IpAddress,
                    Timestamp = forensicTimestamp,
                    Opacity = forensic.Opacity,
                    Rotation = forensic.Rotation,
                },
            };
        }

        var snapshotJson = _layoutService.GenerateLayoutSnapshotJson(
            document,
            options.PageSetup,
            fonts,
            options.ReviewDisplayMode);

        return (new DocumentPdfExportRequest
        {
            DocumentId = request.DocumentId ?? document.DocumentId,
            Document = document,
            FileName = request.FileName,
            Options = options,
            LayoutSnapshotJson = snapshotJson,
        }, fonts, forensicTimestamp);
    }

    private static int PageCountOf(string layoutSnapshotJson)
    {
        using var snapshot = System.Text.Json.JsonDocument.Parse(layoutSnapshotJson);
        return snapshot.RootElement.TryGetProperty("pageCount", out var pageCount) ? pageCount.GetInt32() : 0;
    }
}
