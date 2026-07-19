using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Semantic MCP block/table tools for DocumentEditor documents: insert/delete/move/update blocks
/// and set table-cell text. Positions follow the operation semantics (docs/document-operations-
/// semantics.md): the body is ORDER-VALUE ordered (fractional orders insert between blocks, the
/// moved block wins ties), table cells are index-ordered. Everything compiles into canonical
/// insertBlock/deleteBlock/moveBlock/updateBlock/setBlockAttribute operations applied through the
/// same pipeline as document_editor_apply_operations.
/// </summary>
[McpServerToolType]
public static class DocumentEditorBlockTools
{
    private static readonly string[] SupportedBlockTypes = ["paragraph", "heading", "list", "quote"];

    [McpServerTool(Name = "document_editor_insert_block")]
    [Description("Insert a new text block (paragraph, heading, list, or quote) into the document body or a table cell. Body position uses ORDER-VALUE semantics (fractional 'order' inserts between blocks; omit to append); table cells use list-index semantics ('order' = index, omit to append). Returns the new blockId.")]
    public static async Task<string> InsertBlock(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Block type: paragraph, heading, list, or quote.")] string blockType,
        [Description("Plain text content of the new block.")] string text,
        [Description("Body: order value (fractional inserts between blocks). Table cell: list index. Omit to append at the end.")] double? order = null,
        [Description("Heading level 1-6 when blockType is heading.")] int headingLevel = 1,
        [Description("Whether a list block is ordered (numbered).")] bool ordered = false,
        [Description("Table cell id to insert into a table cell instead of the body.")] string? tableCellId = null,
        [Description("Optional explicit id for the new block; generated when omitted.")] string? blockId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var normalizedType = blockType.Trim().ToLowerInvariant();
        if (!SupportedBlockTypes.Contains(normalizedType))
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Block type '{blockType}' is not supported by document_editor_insert_block. Supported types: {string.Join(", ", SupportedBlockTypes)}. For tables and other structures use document_editor_apply_operations or document_editor_save_document.");
        }

        if (normalizedType == "heading" && headingLevel is < 1 or > 6)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"Heading level {headingLevel} is invalid; use 1-6.");
        }

        double resolvedOrder;
        if (string.IsNullOrWhiteSpace(tableCellId))
        {
            resolvedOrder = order ?? (load.Document.Blocks.Count == 0 ? 0 : load.Document.Blocks.Max(b => b.Order) + 1);
        }
        else
        {
            var cell = DocumentEditorSemanticCore.FindTableCell(load.Document, tableCellId);
            if (cell is null)
            {
                return McpToolResults.Failure(
                    McpToolResults.NotFound,
                    $"Table cell '{tableCellId}' was not found. Use document_editor_describe_document to list table cell ids.");
            }

            resolvedOrder = order ?? cell.Blocks.Count;
        }

        DocumentBlockContent content = normalizedType switch
        {
            "heading" => new HeadingBlockContent { Level = headingLevel, Inlines = [new TextRun { Text = text }] },
            "list" => new ListBlockContent { Ordered = ordered, Inlines = [new TextRun { Text = text }] },
            "quote" => new QuoteBlockContent { Inlines = [new TextRun { Text = text }] },
            _ => new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
        };

        var newBlockId = string.IsNullOrWhiteSpace(blockId) ? Guid.NewGuid().ToString("N") : blockId;
        if (DocumentEditorSemanticCore.FindBlock(load.Document, newBlockId, tableCellId: null) is not null)
        {
            return McpToolResults.Failure(
                McpToolResults.ValidationFailed,
                $"A block with id '{newBlockId}' already exists — inserting it again would be a no-op. Omit blockId to generate a fresh id, or use document_editor_update_block to replace the existing block.");
        }

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.InsertBlock,
            Target = new DocumentOperationTarget { TableCellId = tableCellId, Order = resolvedOrder },
            Block = new DocumentBlock
            {
                Id = newBlockId,
                Type = normalizedType switch
                {
                    "heading" => DocumentBlockType.Heading,
                    "list" => DocumentBlockType.List,
                    "quote" => DocumentBlockType.Quote,
                    _ => DocumentBlockType.Paragraph
                },
                Order = resolvedOrder,
                Content = content
            }
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?>
            {
                ["blockId"] = newBlockId,
                ["order"] = resolvedOrder,
                ["tableCellId"] = tableCellId
            });
    }

    [McpServerTool(Name = "document_editor_delete_block")]
    [Description("Delete a block from the document body or a table cell (address per document_editor_describe_document).")]
    public static async Task<string> DeleteBlock(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Block id to delete.")] string blockId,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (DocumentEditorSemanticCore.FindBlock(load.Document, blockId, tableCellId) is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(blockId, tableCellId);
        }

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.DeleteBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, TableCellId = tableCellId }
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["blockId"] = blockId });
    }

    [McpServerTool(Name = "document_editor_move_block")]
    [Description("Move a block to a new position. Body: ORDER-VALUE semantics — 'order' becomes the block's order and the moved block sorts before blocks already carrying the same order (order 0 puts it first). Table cell: 'order' is the target list index.")]
    public static async Task<string> MoveBlock(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Block id to move.")] string blockId,
        [Description("Body: new order value. Table cell: target index.")] double order,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (DocumentEditorSemanticCore.FindBlock(load.Document, blockId, tableCellId) is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(blockId, tableCellId);
        }

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.MoveBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, TableCellId = tableCellId, Order = order }
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["blockId"] = blockId, ["order"] = order });
    }

    [McpServerTool(Name = "document_editor_update_block")]
    [Description("Replace a whole block in place with a full persistence DocumentBlock JSON payload (PascalCase properties, $type content discriminators — see docs/document-canonical-model.md). The payload id is forced to the addressed blockId. Body order is preserved unless the payload changes it.")]
    public static async Task<string> UpdateBlock(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Block id to replace.")] string blockId,
        [Description("Full replacement DocumentBlock JSON (persistence format).")] string blockJson,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (DocumentEditorSemanticCore.FindBlock(load.Document, blockId, tableCellId) is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(blockId, tableCellId);
        }

        DocumentBlock? block;
        try
        {
            block = JsonSerializer.Deserialize<DocumentBlock>(blockJson, DocumentEditorJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The block JSON could not be parsed: {ex.Message}");
        }

        if (block is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The block JSON is empty.");
        }

        block.Id = blockId;
        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.UpdateBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, TableCellId = tableCellId },
            Block = block
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["blockId"] = blockId });
    }

    [McpServerTool(Name = "document_editor_set_table_cell_text")]
    [Description("Replace the text of a table cell: the cell's first paragraph gets a single text run with the given text (a paragraph is created when the cell is empty). Targets the TABLE block; tableCellId addresses the cell inside it. Compiles to setBlockAttribute table.cell.text.")]
    public static async Task<string> SetTableCellText(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Table block id (the block whose type is table).")] string tableBlockId,
        [Description("Table cell id inside that table.")] string tableCellId,
        [Description("New plain text for the cell.")] string text,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var tableBlock = DocumentEditorSemanticCore.FindBlock(load.Document, tableBlockId, tableCellId: null);
        if (tableBlock is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(tableBlockId, tableCellId: null);
        }

        if (tableBlock.Content is not TableBlockContent table)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Block '{tableBlockId}' is a {tableBlock.Content.GetType().Name}, not a table. Pass the table block id; use document_editor_describe_document to find it.");
        }

        var cellExists = table.Rows.SelectMany(row => row.Cells)
            .Any(cell => string.Equals(cell.Id, tableCellId, StringComparison.Ordinal));
        if (!cellExists)
        {
            return McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Table cell '{tableCellId}' was not found in table '{tableBlockId}'. Use document_editor_describe_document to list cell ids.");
        }

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = tableBlockId, TableCellId = tableCellId },
            AttributeName = "table.cell.text",
            AttributeValueJson = JsonSerializer.Serialize(text)
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["blockId"] = tableBlockId, ["tableCellId"] = tableCellId });
    }
}
