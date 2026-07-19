using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// MCP introspection tool producing a structured, agent-friendly overview of a DocumentEditor
/// document: every block with its stable semantic address (see docs/document-mcp-addressing.md),
/// truncated plain text, tables with cell ids, tokens, content controls and headers/footers.
/// </summary>
[McpServerToolType]
public static class DocumentEditorDescribeTools
{
    private const int DefaultMaxTextLength = 160;
    private const int MaxTextLengthCeiling = 4000;

    [McpServerTool(Name = "document_editor_describe_document")]
    [Description("Describe a DocumentEditor document for agents: blocks with stable addresses (blockId, tableCellId, content-control path), types, truncated plain text, tables with cell ids, tokens, content controls and headers/footers. Returns concurrencyToken (authoritative for saves) and contentDigest (SHA-256 of the normalized snapshot) for optimistic concurrency. Text ranges for operations are expressed as offset/length in a block's plain text, which concatenates ONLY text runs; tokens/fields/drawings are listed separately under 'objects'.")]
    public static async Task<string> DescribeDocument(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Maximum characters of plain text returned per block before truncation with an ellipsis. Negative values clamp to 0.")] int maxTextLength = DefaultMaxTextLength)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions
        {
            IncludeDocument = true,
            IncludeJson = false
        });

        if (!load.Found || load.Document is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, load.ErrorMessage ?? $"DocumentEditor document '{documentId}' not found.");
        }

        var document = load.Document;
        var context = new DescribeContext(Math.Clamp(maxTextLength, 0, MaxTextLengthCeiling));

        var bodyBlocks = document.Blocks
            .OrderBy(b => b.Order).ThenBy(b => b.Id, StringComparer.Ordinal)
            .Select(b => DescribeBlock(b, BlockAddress.Body(b.Id), context))
            .ToList();

        var headersFooters = document.HeadersFooters.Select(hf => new
        {
            id = hf.Id,
            type = hf.Type,
            scope = hf.Scope,
            sectionId = hf.SectionId,
            blocks = hf.Blocks
                .Select(b => DescribeBlock(b, BlockAddress.HeaderFooter(b.Id, hf.Id), context))
                .ToList()
        }).ToList();

        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = load.ConcurrencyToken,
            contentDigest = ComputeContentDigest(document),
            schemaVersion = document.SchemaVersion,
            metadata = new
            {
                title = document.Metadata.Title,
                author = document.Metadata.Author,
                status = document.Metadata.Status
            },
            statistics = new
            {
                bodyBlockCount = document.Blocks.Count,
                totalBlockCount = context.TotalBlockCount,
                tableCount = context.TableCount,
                tokenCount = context.TokenOccurrences.Count,
                contentControlCount = context.ContentControls.Count,
                headerFooterCount = document.HeadersFooters.Count,
                commentCount = document.Comments.Count,
                noteCount = document.Notes.Count,
                revisionCount = document.Revisions.Count
            },
            blocks = bodyBlocks,
            headersFooters,
            tokens = BuildTokenSummaries(context),
            contentControls = context.ContentControls
        });
    }

    /// <summary>SHA-256 hex digest of the normalized persistence JSON snapshot.</summary>
    internal static string ComputeContentDigest(DocumentEditorDocument document)
    {
        var normalized = DocumentEditorJson.Serialize(document);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class DescribeContext(int maxTextLength)
    {
        public int MaxTextLength { get; } = maxTextLength;
        public int TotalBlockCount { get; set; }
        public int TableCount { get; set; }
        public List<TokenOccurrence> TokenOccurrences { get; } = [];
        public List<object> ContentControls { get; } = [];
    }

    private sealed record TokenOccurrence(TokenRun Token, string BlockId, int InlineIndex);

    private sealed record BlockAddress(
        string Container,
        string BlockId,
        string? TableBlockId,
        string? TableCellId,
        string? ContentControlBlockId,
        string? HeaderFooterId,
        bool OperationAddressable)
    {
        public static BlockAddress Body(string blockId)
            => new("body", blockId, null, null, null, null, OperationAddressable: true);

        public static BlockAddress HeaderFooter(string blockId, string headerFooterId)
            => new("headerFooter", blockId, null, null, null, headerFooterId, OperationAddressable: false);

        public BlockAddress InTableCell(string blockId, string tableBlockId, string tableCellId)
            => this with
            {
                Container = "tableCell",
                BlockId = blockId,
                TableBlockId = tableBlockId,
                TableCellId = tableCellId,
                // Deep body resolution descends through table cells only; header/footer and
                // content-control subtrees stay unaddressable for operations.
                OperationAddressable = OperationAddressable
            };

        public BlockAddress InContentControl(string blockId, string contentControlBlockId)
            => this with
            {
                Container = "contentControl",
                BlockId = blockId,
                ContentControlBlockId = contentControlBlockId,
                OperationAddressable = false
            };
    }

    private static object DescribeBlock(DocumentBlock block, BlockAddress address, DescribeContext context)
    {
        context.TotalBlockCount++;

        var addressPayload = new
        {
            container = address.Container,
            blockId = address.BlockId,
            tableBlockId = address.TableBlockId,
            tableCellId = address.TableCellId,
            contentControlBlockId = address.ContentControlBlockId,
            headerFooterId = address.HeaderFooterId,
            operationAddressable = address.OperationAddressable
        };

        switch (block.Content)
        {
            case TableBlockContent table:
                context.TableCount++;
                return new
                {
                    blockId = block.Id,
                    type = "table",
                    order = block.Order,
                    sectionId = block.SectionId,
                    address = addressPayload,
                    rowCount = table.Rows.Count,
                    columnCount = table.Rows.Count == 0 ? 0 : table.Rows.Max(r => r.Cells.Sum(c => Math.Max(1, c.ColumnSpan))),
                    rows = table.Rows.Select((row, rowIndex) => new
                    {
                        index = rowIndex,
                        cells = row.Cells.Select((cell, cellIndex) => new
                        {
                            cellId = cell.Id,
                            index = cellIndex,
                            columnSpan = cell.ColumnSpan,
                            rowSpan = cell.RowSpan,
                            isHeader = cell.IsHeader,
                            blocks = cell.Blocks
                                .Select(nested => DescribeBlock(nested, address.InTableCell(nested.Id, block.Id, cell.Id), context))
                                .ToList()
                        }).ToList()
                    }).ToList()
                };

            case ContentControlBlockContent control:
                context.ContentControls.Add(DescribeContentControl(control.Control, block.Id, inlineIndex: null));
                return new
                {
                    blockId = block.Id,
                    type = "contentControl",
                    order = block.Order,
                    sectionId = block.SectionId,
                    address = addressPayload,
                    controlId = control.Control.ControlId,
                    controlKind = control.Control.Kind,
                    alias = control.Control.Alias,
                    tag = control.Control.Tag,
                    blocks = control.Blocks
                        .Select(nested => DescribeBlock(nested, address.InContentControl(nested.Id, block.Id), context))
                        .ToList()
                };

            case ImageBlockContent image:
                return new
                {
                    blockId = block.Id,
                    type = "image",
                    order = block.Order,
                    sectionId = block.SectionId,
                    address = addressPayload,
                    altText = image.AltText,
                    caption = image.Caption,
                    assetId = image.AssetId,
                    url = image.Url
                };

            case PageBreakBlockContent pageBreak:
                return new
                {
                    blockId = block.Id,
                    type = "pageBreak",
                    order = block.Order,
                    sectionId = block.SectionId,
                    address = addressPayload,
                    breakType = pageBreak.BreakType
                };

            case CodeBlockContent code:
                return new
                {
                    blockId = block.Id,
                    type = "code",
                    order = block.Order,
                    sectionId = block.SectionId,
                    address = addressPayload,
                    language = code.Language,
                    text = Truncate(code.Code, context.MaxTextLength, out var codeTruncated),
                    textLength = code.Code.Length,
                    textTruncated = codeTruncated
                };

            default:
                return DescribeTextBlock(block, addressPayload, context);
        }
    }

    private static object DescribeTextBlock(DocumentBlock block, object addressPayload, DescribeContext context)
    {
        var (type, level, listInfo, inlines) = block.Content switch
        {
            HeadingBlockContent h => ("heading", (int?)h.Level, (object?)null, h.Inlines),
            ListBlockContent l => ("list", null, new { ordered = l.Ordered, indentLevel = l.IndentLevel, isChecked = l.IsChecked }, l.Inlines),
            QuoteBlockContent q => ("quote", null, null, q.Inlines),
            ParagraphBlockContent p => ("paragraph", null, null, p.Inlines),
            _ => (block.Type.ToString().ToLowerInvariant(), null, null, (List<InlineContent>?)[])
        };

        var plainText = string.Concat((inlines ?? []).OfType<TextRun>().Select(r => r.Text));
        var objects = CollectInlineObjects(block.Id, inlines ?? [], context);

        return new
        {
            blockId = block.Id,
            type,
            order = block.Order,
            sectionId = block.SectionId,
            address = addressPayload,
            level,
            list = listInfo,
            text = Truncate(plainText, context.MaxTextLength, out var truncated),
            textLength = plainText.Length,
            textTruncated = truncated,
            objects = objects.Count == 0 ? null : objects
        };
    }

    private static List<object> CollectInlineObjects(string blockId, List<InlineContent> inlines, DescribeContext context)
    {
        var objects = new List<object>();
        for (var index = 0; index < inlines.Count; index++)
        {
            switch (inlines[index])
            {
                case TextRun:
                    break;

                case TokenRun token:
                    context.TokenOccurrences.Add(new TokenOccurrence(token, blockId, index));
                    objects.Add(new
                    {
                        kind = "token",
                        inlineIndex = index,
                        inlineId = token.Id,
                        key = token.Key,
                        displayName = token.DisplayName,
                        expression = token.Expression
                    });
                    break;

                case DocumentFieldRun field:
                    objects.Add(new
                    {
                        kind = "field",
                        inlineIndex = index,
                        inlineId = field.Id,
                        fieldType = field.FieldType,
                        displayText = field.DisplayText
                    });
                    break;

                case DocumentNoteReferenceRun note:
                    objects.Add(new
                    {
                        kind = "noteReference",
                        inlineIndex = index,
                        inlineId = note.Id,
                        noteId = note.NoteId,
                        noteType = note.NoteType
                    });
                    break;

                case DocumentDrawingRun drawing:
                    objects.Add(new
                    {
                        kind = "drawing",
                        inlineIndex = index,
                        inlineId = drawing.Id,
                        objectId = drawing.ObjectId,
                        altText = drawing.AltText
                    });
                    break;

                case DocumentMathRun math:
                    objects.Add(new
                    {
                        kind = "math",
                        inlineIndex = index,
                        inlineId = math.Id,
                        mathId = math.MathId,
                        altText = math.AltText
                    });
                    break;

                case DocumentContentControlRun controlRun:
                    context.ContentControls.Add(DescribeContentControl(controlRun.Control, blockId, index));
                    objects.Add(new
                    {
                        kind = "contentControl",
                        inlineIndex = index,
                        inlineId = controlRun.Id,
                        controlId = controlRun.Control.ControlId,
                        tag = controlRun.Control.Tag
                    });
                    break;

                case DocumentSigningFieldRun signing:
                    objects.Add(new
                    {
                        kind = "signingField",
                        inlineIndex = index,
                        inlineId = signing.Id,
                        uuid = signing.Uuid,
                        fieldType = signing.FieldType,
                        label = signing.Label
                    });
                    break;

                default:
                    objects.Add(new
                    {
                        kind = "unknown",
                        inlineIndex = index,
                        inlineId = inlines[index].Id
                    });
                    break;
            }
        }

        return objects;
    }

    private static object DescribeContentControl(DocumentContentControl control, string blockId, int? inlineIndex)
    {
        control.Metadata.TryGetValue(DocumentAssemblyMetadata.BranchKey, out var branch);
        control.Metadata.TryGetValue(DocumentAssemblyMetadata.ExpressionKey, out var expression);
        control.Metadata.TryGetValue(DocumentAssemblyMetadata.GroupKey, out var group);
        control.Metadata.TryGetValue(DocumentAssemblyMetadata.BindKey, out var bind);
        var hasAssembly = branch is not null || expression is not null || group is not null || bind is not null;

        return new
        {
            controlId = control.ControlId,
            blockId,
            inlineIndex,
            scope = control.Scope,
            kind = control.Kind,
            alias = control.Alias,
            tag = control.Tag,
            isRequired = control.IsRequired,
            lockContent = control.LockContent,
            lockDeletion = control.LockDeletion,
            assembly = hasAssembly ? new { branch, expression, group, bind } : null
        };
    }

    private static List<object> BuildTokenSummaries(DescribeContext context)
    {
        return context.TokenOccurrences
            .GroupBy(o => o.Token.Key, StringComparer.Ordinal)
            .Select(g => (object)new
            {
                key = g.Key,
                displayName = g.First().Token.DisplayName,
                tokenType = g.First().Token.TokenType,
                expression = g.Select(o => o.Token.Expression).FirstOrDefault(e => !string.IsNullOrEmpty(e)),
                fallbackText = g.Select(o => o.Token.FallbackText).FirstOrDefault(f => !string.IsNullOrEmpty(f)),
                occurrences = g.Select(o => new { blockId = o.BlockId, inlineIndex = o.InlineIndex }).ToList()
            })
            .ToList();
    }

    private static string Truncate(string text, int maxLength, out bool truncated)
    {
        truncated = text.Length > maxLength;
        return truncated ? text[..maxLength] + "…" : text;
    }
}
