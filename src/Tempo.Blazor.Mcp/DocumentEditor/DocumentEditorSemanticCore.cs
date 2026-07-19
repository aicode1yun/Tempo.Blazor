using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Shared execution core for the semantic DocumentEditor MCP tools: applies a compiled operation
/// batch through <see cref="DocumentOperationApplier"/>, post-fixes, validates and saves via the
/// provider — the same pipeline as document_editor_apply_operations — and renders the common
/// success/failure envelopes (concurrencyToken + contentDigest included).
/// </summary>
internal static class DocumentEditorSemanticCore
{
    /// <summary>Applies <paramref name="operations"/> to the loaded document and saves.</summary>
    /// <param name="extraSuccessPayload">Optional extra top-level success fields derived from the
    /// saved document (camelCase keys).</param>
    public static async Task<string> ApplyAsync(
        IDocumentEditorProvider documents,
        string documentId,
        DocumentEditorLoadResult load,
        List<DocumentOperation> operations,
        string? expectedConcurrencyToken,
        bool force,
        Func<DocumentEditorDocument, IDictionary<string, object?>>? extraSuccessPayload = null)
    {
        var batch = new DocumentOperationBatch
        {
            DocumentId = documentId,
            Operations = operations
        };

        var working = McpJsonHelpers.Clone(load.Document!, DocumentEditorJson.Options);
        var applyResult = new DocumentOperationApplier().Apply(working, batch);
        if (!applyResult.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, "One or more compiled document operations failed.", applyResult.Errors);
        }

        var postFixWarnings = DocumentEditorMcpPostFixer.Fix(working);
        var validation = DocumentEditorValidationEngine.Validate(working);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The resulting document is invalid; nothing was saved.", validation.Errors);
        }

        var normalized = DocumentEditorJson.Serialize(working);
        var save = await documents.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = documentId,
            Document = working,
            JsonSnapshot = normalized,
            BaseConcurrencyToken = expectedConcurrencyToken,
            ConcurrencyMode = force
                ? DocumentEditorConcurrencyMode.Force
                : string.IsNullOrEmpty(expectedConcurrencyToken)
                    ? DocumentEditorConcurrencyMode.Optional
                    : DocumentEditorConcurrencyMode.Required,
            NormalizeJson = true
        });

        if (save.Conflict)
        {
            return McpToolResults.Failure(
                McpToolResults.Conflict,
                "The document was modified since you read it. Re-read with document_editor_describe_document and retry.");
        }

        if (!save.Success)
        {
            return McpToolResults.Failure(McpToolResults.Error, save.ErrorMessage ?? "The document could not be saved.");
        }

        var savedDocument = save.Document ?? working;
        var payload = new Dictionary<string, object?>
        {
            ["id"] = documentId,
            ["applied"] = batch.Operations.Count,
            ["concurrencyToken"] = save.ConcurrencyToken,
            ["contentDigest"] = DocumentEditorDescribeTools.ComputeContentDigest(savedDocument),
            ["postFixWarnings"] = DocumentEditorMcpPostFixer.ToToolWarnings(postFixWarnings)
        };

        if (extraSuccessPayload is not null)
        {
            foreach (var pair in extraSuccessPayload(savedDocument))
            {
                payload[pair.Key] = pair.Value;
            }
        }

        return McpToolResults.Success(payload);
    }

    /// <summary>Standard not-found failure for a missing document.</summary>
    public static string DocumentNotFound(DocumentEditorLoadResult load, string documentId)
        => McpToolResults.Failure(McpToolResults.NotFound, load.ErrorMessage ?? $"DocumentEditor document '{documentId}' not found.");

    /// <summary>Standard not-found failure for a missing block address, with the describe hint.</summary>
    public static string BlockNotFound(string blockId, string? tableCellId)
        => McpToolResults.Failure(
            McpToolResults.NotFound,
            $"Block '{blockId}' was not found in the document body or its table cells"
            + (string.IsNullOrWhiteSpace(tableCellId) ? "" : $" (table cell '{tableCellId}')")
            + ". Use document_editor_describe_document to list block addresses; blocks inside content controls or headers/footers are not operation-addressable.");

    /// <summary>
    /// Deep block resolution mirroring DocumentOperationApplier.FindBlockLocation: body blocks
    /// first, then recursively through table cells; tableCellId, when supplied, restricts which
    /// container may match.
    /// </summary>
    public static DocumentBlock? FindBlock(DocumentEditorDocument document, string blockId, string? tableCellId)
    {
        return Visit(document.Blocks, string.Empty);

        DocumentBlock? Visit(List<DocumentBlock> blocks, string cellId)
        {
            foreach (var block in blocks)
            {
                if (string.Equals(block.Id, blockId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(tableCellId) || string.Equals(tableCellId, cellId, StringComparison.Ordinal)))
                {
                    return block;
                }

                if (block.Content is not TableBlockContent table)
                {
                    continue;
                }

                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        if (Visit(cell.Blocks, cell.Id ?? string.Empty) is { } nested)
                        {
                            return nested;
                        }
                    }
                }
            }

            return null;
        }
    }

    /// <summary>Finds a table cell anywhere in the body (mirrors FindContainerBlocks).</summary>
    public static TableCellContent? FindTableCell(DocumentEditorDocument document, string tableCellId)
    {
        return Visit(document.Blocks);

        TableCellContent? Visit(List<DocumentBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block.Content is not TableBlockContent table)
                {
                    continue;
                }

                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        if (string.Equals(cell.Id, tableCellId, StringComparison.Ordinal))
                        {
                            return cell;
                        }

                        if (Visit(cell.Blocks) is { } nested)
                        {
                            return nested;
                        }
                    }
                }
            }

            return null;
        }
    }

    /// <summary>Inline list of text-like block content, null for non-text blocks.</summary>
    public static List<InlineContent>? GetInlineList(DocumentBlockContent content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    /// <summary>Plain text of a block per the addressing contract: text runs only.</summary>
    public static string PlainTextOf(List<InlineContent>? inlines)
        => string.Concat((inlines ?? []).OfType<TextRun>().Select(r => r.Text));
}
