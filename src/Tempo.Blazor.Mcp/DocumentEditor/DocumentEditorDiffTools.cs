using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.DocumentFormats.Redline;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// MCP diff/redline tools: compare two saved versions of a document (or a version against the
/// current state) into an agent-friendly structured diff (<see cref="DocumentComparisonService"/>),
/// and optionally export the comparison as a redline — DOCX with real w:ins/w:del tracked changes
/// (<see cref="DocumentRedlineDocxExporter"/>) or a PDF rendered with review markup through the
/// headless pipeline.
/// </summary>
[McpServerToolType]
public static class DocumentEditorDiffTools
{
    [McpServerTool(Name = "document_editor_diff_versions")]
    [Description("Compare two saved versions of a DocumentEditor document — or a version against the CURRENT state (omit compareVersionId) — into a structured diff: summary counters, per-block changes (added/removed/changed) with old/new text and word-level diff segments. redlineAvailable signals whether document_editor_export_redline has anything to export.")]
    public static async Task<string> DiffVersions(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Base (older) version id — see document_editor_get_versions.")] string baseVersionId,
        [Description("Compared (newer) version id; omit to compare against the current document state.")] string? compareVersionId = null)
    {
        var resolve = await ResolveComparisonAsync(documents, documentId, baseVersionId, compareVersionId);
        if (resolve.Error is not null)
        {
            return resolve.Error;
        }

        var result = new DocumentComparisonService().Compare(resolve.BaseDocument!, resolve.CompareDocument!);
        if (!result.Success)
        {
            return McpToolResults.Failure(McpToolResults.Error, result.ErrorMessage ?? "The comparison failed.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            baseVersionId,
            compareVersionId,
            comparedAgainstCurrent = compareVersionId is null,
            summary = new
            {
                addedBlocks = result.Summary.AddedBlocks,
                removedBlocks = result.Summary.RemovedBlocks,
                changedBlocks = result.Summary.ChangedBlocks,
                hasChanges = result.Summary.HasChanges
            },
            redlineAvailable = result.Summary.HasChanges,
            changes = result.Changes.Select(change => new
            {
                kind = change.Kind,
                blockId = change.BlockId,
                oldText = change.OldText,
                newText = change.NewText,
                textDiff = change.TextDiff.Segments.Select(segment => new
                {
                    kind = segment.Kind,
                    text = segment.Text
                }).ToList()
            }).ToList()
        });
    }

    [McpServerTool(Name = "document_editor_export_redline")]
    [Description("Export the diff of two versions (or a version vs. the current state) as a redline: format docx produces a Word document with REAL tracked changes (w:ins/w:del, reviewable in Word), format pdf renders the tracked-changes document with review markup through the headless pipeline. Fails with invalid_operation when the versions are identical — nothing to redline.")]
    public static async Task<string> ExportRedline(
        IDocumentEditorProvider documents,
        ITempoDocumentService renderer,
        ITempoDocumentMcpFontCatalog fontCatalog,
        TempoDocumentMcpRenderOptions renderOptions,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Base (older) version id.")] string baseVersionId,
        [Description("Compared (newer) version id; omit to compare against the current document state.")] string? compareVersionId = null,
        [Description("Output format: docx (tracked changes) or pdf (review markup render).")] string format = "docx",
        [Description("Author name stamped on the tracked changes.")] string? authorName = null,
        [Description("ISO-8601 timestamp stamped on the tracked changes; defaults to now.")] string? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("docx" or "pdf"))
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, $"Format '{format}' is not supported by document_editor_export_redline; use docx or pdf.");
        }

        DateTimeOffset? parsedTimestamp = null;
        if (!string.IsNullOrWhiteSpace(timestamp))
        {
            if (!DateTimeOffset.TryParse(timestamp, out var parsed))
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"Timestamp '{timestamp}' is not a valid ISO-8601 date/time.");
            }

            parsedTimestamp = parsed;
        }

        var resolve = await ResolveComparisonAsync(documents, documentId, baseVersionId, compareVersionId);
        if (resolve.Error is not null)
        {
            return resolve.Error;
        }

        var result = new DocumentComparisonService().Compare(resolve.BaseDocument!, resolve.CompareDocument!);
        if (!result.Success)
        {
            return McpToolResults.Failure(McpToolResults.Error, result.ErrorMessage ?? "The comparison failed.");
        }

        if (!result.Summary.HasChanges)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                "The compared versions are identical — there is nothing to redline. Use document_editor_diff_versions to check redlineAvailable first.");
        }

        var redlineOptions = new DocumentRedlineOptions
        {
            Author = new DocumentEditorAuthor
            {
                Id = "mcp-agent",
                DisplayName = string.IsNullOrWhiteSpace(authorName) ? "MCP Agent" : authorName
            },
            Timestamp = parsedTimestamp ?? DateTimeOffset.UtcNow
        };

        try
        {
            if (normalizedFormat == "docx")
            {
                var export = await new DocumentRedlineDocxExporter().ExportAsync(result, redlineOptions, cancellationToken);
                return McpToolResults.Success(new
                {
                    id = documentId,
                    baseVersionId,
                    compareVersionId,
                    format = "docx",
                    contentType = export.ContentType,
                    fileName = export.FileName,
                    contentBase64 = Convert.ToBase64String(export.Content),
                    exportWarnings = export.Warnings.Select(w => new { severity = w.Severity, code = w.Code, message = w.Message }).ToList()
                });
            }

            if (fontCatalog.Fonts.Count == 0)
            {
                return McpToolResults.Failure(
                    McpToolResults.Unsupported,
                    "No fonts are configured for headless rendering. Register fonts via AddTempoDocumentEditorMcpRendering(options => options.Fonts.Add(...)) or enable IncludeSystemFontFallback.");
            }

            var redline = new DocumentRedlineBuilder().Build(result, redlineOptions);
            var pdf = await renderer.RenderPdfAsync(new TempoDocumentRenderRequest
            {
                Document = redline,
                Fonts = fontCatalog.Fonts,
                DocumentId = redline.DocumentId,
                ImageResolver = renderOptions.ImageResolver,
                Options = new DocumentPdfExportOptions
                {
                    ReviewDisplayMode = DocumentReviewDisplayMode.AllMarkup
                }
            }, cancellationToken);

            return McpToolResults.Success(new
            {
                id = documentId,
                baseVersionId,
                compareVersionId,
                format = "pdf",
                contentType = "application/pdf",
                pageCount = pdf.PageCount,
                contentBase64 = Convert.ToBase64String(pdf.PdfContent)
            });
        }
        catch (TempoDocumentLayoutException ex)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"{ex.Message} Configure the missing font faces via AddTempoDocumentEditorMcpRendering options.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(McpToolResults.Error, $"The redline could not be exported: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- helpers

    private sealed record ResolvedComparison(DocumentEditorDocument? BaseDocument, DocumentEditorDocument? CompareDocument, string? Error);

    private static async Task<ResolvedComparison> ResolveComparisonAsync(
        IDocumentEditorProvider documents,
        string documentId,
        string baseVersionId,
        string? compareVersionId)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return new ResolvedComparison(null, null, DocumentEditorSemanticCore.DocumentNotFound(load, documentId));
        }

        var versions = await documents.GetVersionsAsync(documentId);
        var baseDocument = FromVersion(versions, baseVersionId);
        if (baseDocument is null)
        {
            return new ResolvedComparison(null, null, McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Version '{baseVersionId}' was not found. Use document_editor_get_versions to list saved versions."));
        }

        DocumentEditorDocument? compareDocument;
        if (string.IsNullOrWhiteSpace(compareVersionId))
        {
            compareDocument = load.Document;
        }
        else
        {
            compareDocument = FromVersion(versions, compareVersionId);
            if (compareDocument is null)
            {
                return new ResolvedComparison(null, null, McpToolResults.Failure(
                    McpToolResults.NotFound,
                    $"Version '{compareVersionId}' was not found. Use document_editor_get_versions to list saved versions."));
            }
        }

        return new ResolvedComparison(baseDocument, compareDocument, null);
    }

    private static DocumentEditorDocument? FromVersion(IReadOnlyList<DocumentVersion> versions, string versionId)
    {
        var version = versions.FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal));
        if (version is null || string.IsNullOrWhiteSpace(version.Snapshot.Json))
        {
            return null;
        }

        try
        {
            return DocumentEditorJson.Deserialize(version.Snapshot.Json);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
