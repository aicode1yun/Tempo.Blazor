using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Visual-feedback MCP tools: render a DocumentEditor document to per-page PNG previews or a
/// WYSIWYG PDF via the headless document pipeline (<see cref="ITempoDocumentService"/> — Jint
/// canvas layout + Skia). Fail-closed: when the configured font catalog cannot measure the
/// document, the tools return an agent-friendly error instead of silently rendering with
/// synthetic metrics.
/// </summary>
[McpServerToolType]
public static class DocumentEditorRenderTools
{
    private const double MinDpi = 24;
    private const double MaxDpi = 600;

    [McpServerTool(Name = "document_render_preview")]
    [Description("Render a DocumentEditor document (by id or inline JSON) to per-page PNG previews — the agent's visual feedback channel after edits. Parameters: pages (1-based selection like '1,3-5'; omit for all), dpi (24-600, 96 = CSS pixels), maxPages cap. Returns base64 PNG pages with dimensions. Fails closed with a font-catalog diagnostic when the configured fonts cannot measure the document (configure AddTempoDocumentEditorMcpRendering).")]
    public static async Task<string> RenderPreview(
        IDocumentEditorProvider documents,
        ITempoDocumentService renderer,
        ITempoDocumentMcpFontCatalog fontCatalog,
        TempoDocumentMcpRenderOptions renderOptions,
        [Description("DocumentEditor document id. Required when documentJson is omitted.")] string? documentId = null,
        [Description("Optional full document JSON to render without loading from the provider.")] string? documentJson = null,
        [Description("1-based page selection, e.g. '1', '2-4', '1,3-5'. Omit for all pages (subject to maxPages).")] string? pages = null,
        [Description("Raster DPI; 96 = CSS pixel scale, higher = sharper preview.")] double dpi = 96,
        [Description("Maximum pages returned in one call; 0 uses the configured MaxPreviewPages.")] int maxPages = 0,
        CancellationToken cancellationToken = default)
    {
        if (dpi is < MinDpi or > MaxDpi)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"dpi {dpi} is out of range; use {MinDpi}-{MaxDpi}.");
        }

        var resolve = await ResolveDocumentAsync(documents, documentId, documentJson);
        if (resolve.Error is not null)
        {
            return resolve.Error;
        }

        if (fontCatalog.Fonts.Count == 0)
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "No fonts are configured for headless rendering. Register fonts via AddTempoDocumentEditorMcpRendering(options => options.Fonts.Add(...)) or enable IncludeSystemFontFallback.");
        }

        var request = new TempoDocumentRenderRequest
        {
            Document = resolve.Document!,
            Fonts = fontCatalog.Fonts,
            DocumentId = resolve.Document!.DocumentId,
            ImageResolver = renderOptions.ImageResolver
        };

        IReadOnlyList<TempoDocumentPageImage> allPages;
        try
        {
            allPages = await renderer.RenderPageImagesAsync(request, dpi, cancellationToken);
        }
        catch (TempoDocumentLayoutException ex)
        {
            return FontDiagnosticsFailure(ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(McpToolResults.Error, $"The document could not be rendered: {ex.Message}");
        }

        var selection = ParsePageSelection(pages, allPages.Count, out var selectionError);
        if (selectionError is not null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, selectionError);
        }

        var cap = maxPages > 0 ? maxPages : renderOptions.MaxPreviewPages;
        var truncated = selection.Count > cap;
        var rendered = selection.Take(cap).Select(index => allPages[index]).Select(page => new
        {
            pageNumber = page.PageIndex + 1,
            width = page.Width,
            height = page.Height,
            contentType = "image/png",
            base64 = Convert.ToBase64String(page.Png)
        }).ToList();

        return McpToolResults.Success(new
        {
            id = resolve.Document!.DocumentId,
            concurrencyToken = resolve.ConcurrencyToken,
            contentDigest = DocumentEditorDescribeTools.ComputeContentDigest(resolve.Document!),
            pageCount = allPages.Count,
            dpi,
            truncated,
            renderedPages = rendered
        });
    }

    [McpServerTool(Name = "document_render_pdf")]
    [Description("Render a DocumentEditor document (by id or inline JSON) to a WYSIWYG PDF. exportOptionsJson passes DocumentPdfExportOptions through (page setup, review display mode, comments/suggestions toggles, forensic watermark). Returns base64 PDF bytes, pageCount and the forensic timestamp when stamped. Fails closed on font-catalog gaps.")]
    public static async Task<string> RenderPdf(
        IDocumentEditorProvider documents,
        ITempoDocumentService renderer,
        ITempoDocumentMcpFontCatalog fontCatalog,
        TempoDocumentMcpRenderOptions renderOptions,
        [Description("DocumentEditor document id. Required when documentJson is omitted.")] string? documentId = null,
        [Description("Optional full document JSON to render without loading from the provider.")] string? documentJson = null,
        [Description("Optional DocumentPdfExportOptions JSON (persistence casing): PageSetup, ReviewDisplayMode, IncludeComments, IncludeSuggestions, ForensicWatermark.")] string? exportOptionsJson = null,
        [Description("Suggested file name without extension.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var resolve = await ResolveDocumentAsync(documents, documentId, documentJson);
        if (resolve.Error is not null)
        {
            return resolve.Error;
        }

        if (fontCatalog.Fonts.Count == 0)
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "No fonts are configured for headless rendering. Register fonts via AddTempoDocumentEditorMcpRendering(options => options.Fonts.Add(...)) or enable IncludeSystemFontFallback.");
        }

        DocumentPdfExportOptions? exportOptions = null;
        if (!string.IsNullOrWhiteSpace(exportOptionsJson))
        {
            try
            {
                exportOptions = JsonSerializer.Deserialize<DocumentPdfExportOptions>(exportOptionsJson, DocumentEditorJson.Options);
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"exportOptionsJson could not be parsed: {ex.Message}");
            }
        }

        var request = new TempoDocumentRenderRequest
        {
            Document = resolve.Document!,
            Fonts = fontCatalog.Fonts,
            Options = exportOptions,
            DocumentId = resolve.Document!.DocumentId,
            FileName = fileName,
            ImageResolver = renderOptions.ImageResolver
        };

        try
        {
            var result = await renderer.RenderPdfAsync(request, cancellationToken);
            return McpToolResults.Success(new
            {
                id = resolve.Document!.DocumentId,
                concurrencyToken = resolve.ConcurrencyToken,
                contentDigest = DocumentEditorDescribeTools.ComputeContentDigest(resolve.Document!),
                pageCount = result.PageCount,
                contentType = "application/pdf",
                fileName = string.IsNullOrWhiteSpace(fileName) ? resolve.Document!.DocumentId : fileName,
                forensicTimestamp = result.ForensicTimestamp,
                base64 = Convert.ToBase64String(result.PdfContent)
            });
        }
        catch (TempoDocumentLayoutException ex)
        {
            return FontDiagnosticsFailure(ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(McpToolResults.Error, $"The document could not be rendered to PDF: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- helpers

    private sealed record ResolvedDocument(DocumentEditorDocument? Document, string? ConcurrencyToken, string? Error);

    private static async Task<ResolvedDocument> ResolveDocumentAsync(
        IDocumentEditorProvider documents,
        string? documentId,
        string? documentJson)
    {
        if (!string.IsNullOrWhiteSpace(documentJson))
        {
            try
            {
                var document = DocumentEditorJson.Deserialize(documentJson);
                return new ResolvedDocument(document, null, null);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return new ResolvedDocument(null, null,
                    McpToolResults.Failure(McpToolResults.ValidationFailed, $"documentJson could not be parsed: {ex.Message}"));
            }
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return new ResolvedDocument(null, null,
                McpToolResults.Failure(McpToolResults.ValidationFailed, "Pass either documentId or documentJson."));
        }

        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return new ResolvedDocument(null, null, DocumentEditorSemanticCore.DocumentNotFound(load, documentId));
        }

        return new ResolvedDocument(load.Document, load.ConcurrencyToken, null);
    }

    private static string FontDiagnosticsFailure(TempoDocumentLayoutException ex)
        => McpToolResults.Failure(
            McpToolResults.InvalidOperation,
            $"{ex.Message} Configure the missing font faces (or aliases mapping the family to an available face) via AddTempoDocumentEditorMcpRendering options, or change the document's font family with document_editor_format_range/theme settings.");

    /// <summary>Parses '1,3-5' style 1-based selections into zero-based page indexes.</summary>
    private static List<int> ParsePageSelection(string? pages, int pageCount, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(pages))
        {
            return Enumerable.Range(0, pageCount).ToList();
        }

        var indexes = new List<int>();
        foreach (var part in pages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length == 1 && int.TryParse(bounds[0], out var single))
            {
                if (single < 1 || single > pageCount)
                {
                    error = $"Page {single} is out of range; the document has {pageCount} page(s).";
                    return [];
                }

                indexes.Add(single - 1);
                continue;
            }

            if (bounds.Length == 2 && int.TryParse(bounds[0], out var from) && int.TryParse(bounds[1], out var to) && from <= to)
            {
                if (from < 1 || to > pageCount)
                {
                    error = $"Page range {part} is out of range; the document has {pageCount} page(s).";
                    return [];
                }

                indexes.AddRange(Enumerable.Range(from - 1, to - from + 1));
                continue;
            }

            error = $"Page selection '{part}' is invalid; use forms like '1', '2-4', '1,3-5'.";
            return [];
        }

        return indexes.Distinct().Order().ToList();
    }
}
