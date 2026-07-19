using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Semantic MCP text/formatting tools for DocumentEditor documents. Each tool addresses a block by
/// its stable address (blockId + optional tableCellId, see docs/document-mcp-addressing.md) and a
/// text range as offset/length in the block's PLAIN TEXT (concatenated text runs only). The tools
/// compile into canonical insertText/deleteText/addInlineMark/removeInlineMark/setBlockAttribute
/// operations and apply them through <see cref="DocumentOperationApplier"/> + provider save — the
/// exact same path as document_editor_apply_operations, so collaboration semantics converge.
/// </summary>
[McpServerToolType]
public static class DocumentEditorSemanticTextTools
{
    [McpServerTool(Name = "document_editor_insert_text")]
    [Description("Insert text into a block at a plain-text offset (0-based, counts only text runs — see document_editor_describe_document textLength). Address nested table-cell blocks with tableCellId. Pass expectedConcurrencyToken from describe/get to avoid overwriting concurrent edits.")]
    public static Task<string> InsertText(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Plain-text offset to insert at (0..textLength).")] int offset,
        [Description("Text to insert.")] string text,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileInsert(inlines, target, offset, text));

    [McpServerTool(Name = "document_editor_delete_text")]
    [Description("Delete a plain-text range (offset/length, counts only text runs) from a block. Tokens/fields inside the range are preserved — they occupy no plain-text characters. Address nested table-cell blocks with tableCellId.")]
    public static Task<string> DeleteText(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Plain-text range start (0-based).")] int offset,
        [Description("Plain-text range length (must be > 0).")] int length,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileDelete(inlines, target, offset, length, requirePositiveLength: true));

    [McpServerTool(Name = "document_editor_replace_text")]
    [Description("Replace a plain-text range (offset/length) in a block with new text. length=0 behaves as insert. Tokens/fields inside the range are preserved. Address nested table-cell blocks with tableCellId.")]
    public static Task<string> ReplaceText(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Plain-text range start (0-based).")] int offset,
        [Description("Plain-text range length (0 = pure insert).")] int length,
        [Description("Replacement text.")] string text,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileReplace(inlines, target, offset, length, text));

    [McpServerTool(Name = "document_editor_format_range")]
    [Description("Add or remove a formatting mark on a plain-text range of a block. Supported marks: bold, italic, underline, strikethrough, superscript, subscript, smallCaps, allCaps, doubleStrikethrough, characterSpacing, characterScale, kerning, highlight, textColor, fontFamily, fontSize, link. Value-carrying marks (link URL, highlight/textColor color, fontFamily name, fontSize, spacing values) require 'value' when adding. Tokens fully inside the range are marked too.")]
    public static Task<string> FormatRange(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Plain-text range start (0-based).")] int offset,
        [Description("Plain-text range length (must be > 0).")] int length,
        [Description("Mark type, e.g. bold, italic, link, highlight.")] string mark,
        [Description("'add' (default) or 'remove'.")] string action = "add",
        [Description("Mark value for value-carrying marks: link URL, color, font family, font size…")] string? value = null,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileFormatRange(inlines, target, offset, length, mark, action, value));

    [McpServerTool(Name = "document_editor_set_heading")]
    [Description("Convert a text block to a heading of the given level (1-6), preserving its inline content. Compiles to setBlockAttribute headingLevel.")]
    public static Task<string> SetHeading(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Heading level 1-6.")] int level,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileSetHeading(target, level));

    [McpServerTool(Name = "document_editor_set_paragraph_properties")]
    [Description("Patch paragraph-level formatting of a text block: alignment (left|center|right|justify), lineSpacing (0.5-4), spacingBefore/spacingAfter (points), leftIndent/rightIndent/firstLineIndent (points; negative firstLineIndent = hanging indent). Only supplied values change. Compiles to setBlockAttribute paragraphProperties.")]
    public static Task<string> SetParagraphProperties(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target block id.")] string blockId,
        [Description("Text alignment: left, center, right, or justify.")] string? alignment = null,
        [Description("Line spacing multiplier, e.g. 1, 1.15, 1.5, 2.")] double? lineSpacing = null,
        [Description("Spacing before the paragraph in points.")] double? spacingBefore = null,
        [Description("Spacing after the paragraph in points.")] double? spacingAfter = null,
        [Description("Left indent in points.")] double? leftIndent = null,
        [Description("Right indent in points.")] double? rightIndent = null,
        [Description("First-line indent in points (negative = hanging indent).")] double? firstLineIndent = null,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
        => ExecuteAsync(documents, documentId, blockId, tableCellId, expectedConcurrencyToken, force, collaborationBridge,
            (inlines, target) => CompileParagraphProperties(
                target, alignment, lineSpacing, spacingBefore, spacingAfter, leftIndent, rightIndent, firstLineIndent));

    // ---------------------------------------------------------------- execution core

    private sealed record Compilation(List<DocumentOperation>? Operations, string? ErrorCode, string? ErrorMessage)
    {
        public static Compilation Ok(params DocumentOperation[] operations) => new([.. operations], null, null);
        public static Compilation Ok(List<DocumentOperation> operations) => new(operations, null, null);
        public static Compilation Fail(string code, string message) => new(null, code, message);
    }

    private static async Task<string> ExecuteAsync(
        IDocumentEditorProvider documents,
        string documentId,
        string blockId,
        string? tableCellId,
        string? expectedConcurrencyToken,
        bool force,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge,
        Func<List<InlineContent>, DocumentOperationTarget, Compilation> compile)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions
        {
            IncludeDocument = true,
            IncludeJson = false
        });

        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var block = DocumentEditorSemanticCore.FindBlock(load.Document, blockId, tableCellId);
        if (block is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(blockId, tableCellId);
        }

        var inlines = DocumentEditorSemanticCore.GetInlineList(block.Content);
        if (inlines is null)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Block '{blockId}' is a {block.Content.GetType().Name} and has no inline text. Semantic text tools target paragraph, heading, list, and quote blocks.");
        }

        var target = new DocumentOperationTarget { BlockId = blockId, TableCellId = tableCellId };
        var compilation = compile(inlines, target);
        if (compilation.Operations is null)
        {
            return McpToolResults.Failure(compilation.ErrorCode ?? McpToolResults.Error, compilation.ErrorMessage ?? "The request could not be compiled into operations.");
        }

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents,
            documentId,
            load,
            compilation.Operations,
            expectedConcurrencyToken,
            force,
            savedDocument =>
            {
                var savedBlock = DocumentEditorSemanticCore.FindBlock(savedDocument, blockId, tableCellId);
                var savedPlainText = savedBlock is null
                    ? null
                    : DocumentEditorSemanticCore.PlainTextOf(DocumentEditorSemanticCore.GetInlineList(savedBlock.Content));
                return new Dictionary<string, object?>
                {
                    ["blockId"] = blockId,
                    ["blockPlainText"] = savedPlainText,
                    ["blockTextLength"] = savedPlainText?.Length
                };
            },
            collaborationBridge);
    }

    // ---------------------------------------------------------------- compilers

    private static Compilation CompileInsert(List<InlineContent> inlines, DocumentOperationTarget target, int offset, string text)
    {
        var total = PlainTextOf(inlines).Length;
        if (offset < 0 || offset > total)
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                $"Insert offset {offset} is outside the block's plain text (textLength {total}).");
        }

        var insertion = MapInsertionPoint(inlines, offset);
        return Compilation.Ok(new DocumentOperation
        {
            Type = DocumentOperationType.InsertText,
            Target = new DocumentOperationTarget
            {
                BlockId = target.BlockId,
                TableCellId = target.TableCellId,
                InlineIndex = insertion.InlineIndex,
                Offset = insertion.LocalOffset
            },
            Text = text
        });
    }

    private static Compilation CompileDelete(
        List<InlineContent> inlines, DocumentOperationTarget target, int offset, int length, bool requirePositiveLength)
    {
        var total = PlainTextOf(inlines).Length;
        if (requirePositiveLength && length <= 0)
        {
            return Compilation.Fail(McpToolResults.ValidationFailed, "Delete length must be greater than 0.");
        }

        if (offset < 0 || length < 0 || offset + length > total)
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                $"Range offset {offset} + length {length} is outside the block's plain text (textLength {total}).");
        }

        var operations = MapDeleteSegments(inlines, offset, length)
            .Select(segment => new DocumentOperation
            {
                Type = DocumentOperationType.DeleteText,
                Target = new DocumentOperationTarget
                {
                    BlockId = target.BlockId,
                    TableCellId = target.TableCellId,
                    InlineIndex = segment.InlineIndex,
                    Offset = segment.LocalStart,
                    Length = segment.LocalLength
                }
            })
            .ToList();
        return Compilation.Ok(operations);
    }

    private static Compilation CompileReplace(
        List<InlineContent> inlines, DocumentOperationTarget target, int offset, int length, string text)
    {
        var total = PlainTextOf(inlines).Length;
        if (offset < 0 || length < 0 || offset + length > total)
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                $"Range offset {offset} + length {length} is outside the block's plain text (textLength {total}).");
        }

        var operations = new List<DocumentOperation>();
        InsertionPoint insertion;
        if (length > 0)
        {
            var segments = MapDeleteSegments(inlines, offset, length);
            operations.AddRange(segments.Select(segment => new DocumentOperation
            {
                Type = DocumentOperationType.DeleteText,
                Target = new DocumentOperationTarget
                {
                    BlockId = target.BlockId,
                    TableCellId = target.TableCellId,
                    InlineIndex = segment.InlineIndex,
                    Offset = segment.LocalStart,
                    Length = segment.LocalLength
                }
            }));
            // Insert where the deleted range started; after the per-run deletions that run still
            // exists (deleteText never removes runs), so the index/offset stay valid.
            insertion = new InsertionPoint(segments[0].InlineIndex, segments[0].LocalStart);
        }
        else
        {
            insertion = MapInsertionPoint(inlines, offset);
        }

        if (!string.IsNullOrEmpty(text))
        {
            operations.Add(new DocumentOperation
            {
                Type = DocumentOperationType.InsertText,
                Target = new DocumentOperationTarget
                {
                    BlockId = target.BlockId,
                    TableCellId = target.TableCellId,
                    InlineIndex = insertion.InlineIndex,
                    Offset = insertion.LocalOffset
                },
                Text = text
            });
        }

        if (operations.Count == 0)
        {
            return Compilation.Fail(McpToolResults.ValidationFailed, "Replace with length 0 and empty text is a no-op.");
        }

        return Compilation.Ok(operations);
    }

    private static readonly InlineMarkType[] SupportedMarks =
    [
        InlineMarkType.Bold,
        InlineMarkType.Italic,
        InlineMarkType.Underline,
        InlineMarkType.Strikethrough,
        InlineMarkType.Superscript,
        InlineMarkType.Subscript,
        InlineMarkType.SmallCaps,
        InlineMarkType.AllCaps,
        InlineMarkType.DoubleStrikethrough,
        InlineMarkType.CharacterSpacing,
        InlineMarkType.CharacterScale,
        InlineMarkType.Kerning,
        InlineMarkType.Highlight,
        InlineMarkType.TextColor,
        InlineMarkType.FontFamily,
        InlineMarkType.FontSize,
        InlineMarkType.Link
    ];

    private static readonly InlineMarkType[] ValueCarryingMarks =
    [
        InlineMarkType.CharacterSpacing,
        InlineMarkType.CharacterScale,
        InlineMarkType.Kerning,
        InlineMarkType.Highlight,
        InlineMarkType.TextColor,
        InlineMarkType.FontFamily,
        InlineMarkType.FontSize,
        InlineMarkType.Link
    ];

    private static Compilation CompileFormatRange(
        List<InlineContent> inlines, DocumentOperationTarget target, int offset, int length, string mark, string action, string? value)
    {
        var add = action.Equals("add", StringComparison.OrdinalIgnoreCase);
        if (!add && !action.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return Compilation.Fail(McpToolResults.ValidationFailed, $"Action '{action}' is not supported; use 'add' or 'remove'.");
        }

        if (!Enum.TryParse<InlineMarkType>(mark, ignoreCase: true, out var markType) || !SupportedMarks.Contains(markType))
        {
            var supported = string.Join(", ", SupportedMarks.Select(CamelCase));
            return Compilation.Fail(
                McpToolResults.InvalidOperation,
                $"Mark '{mark}' is not supported by document_editor_format_range. Supported marks: {supported}.");
        }

        if (add && ValueCarryingMarks.Contains(markType) && string.IsNullOrWhiteSpace(value))
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                $"Mark '{CamelCase(markType)}' carries a value — pass 'value' (e.g. a URL for link, a color for highlight/textColor).");
        }

        var total = PlainTextOf(inlines).Length;
        if (length <= 0 || offset < 0 || offset + length > total)
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                $"Range offset {offset} + length {length} is outside the block's plain text (textLength {total}; length must be > 0).");
        }

        // The applier's mark range coordinates count token/note display text (GetInlineText space);
        // convert the plain-text boundaries so ranges line up when non-text inlines are present.
        var applierStart = MapPlainToApplierBoundary(inlines, offset, isEnd: false);
        var applierEnd = MapPlainToApplierBoundary(inlines, offset + length, isEnd: true);

        var inlineMark = new InlineMark { Type = markType };
        if (markType == InlineMarkType.Link)
        {
            inlineMark.Link = string.IsNullOrWhiteSpace(value) ? null : new LinkMarkData { Href = value };
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            inlineMark.Value = value;
        }

        return Compilation.Ok(new DocumentOperation
        {
            Type = add ? DocumentOperationType.AddInlineMark : DocumentOperationType.RemoveInlineMark,
            Target = new DocumentOperationTarget
            {
                BlockId = target.BlockId,
                TableCellId = target.TableCellId,
                Offset = applierStart,
                Length = applierEnd - applierStart
            },
            Mark = inlineMark
        });
    }

    private static Compilation CompileSetHeading(DocumentOperationTarget target, int level)
    {
        if (level is < 1 or > 6)
        {
            return Compilation.Fail(McpToolResults.ValidationFailed, $"Heading level {level} is invalid; use 1-6.");
        }

        return Compilation.Ok(new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = target.BlockId, TableCellId = target.TableCellId },
            AttributeName = "headingLevel",
            AttributeValueJson = level.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static Compilation CompileParagraphProperties(
        DocumentOperationTarget target,
        string? alignment,
        double? lineSpacing,
        double? spacingBefore,
        double? spacingAfter,
        double? leftIndent,
        double? rightIndent,
        double? firstLineIndent)
    {
        DocumentTextAlignment? parsedAlignment = null;
        if (!string.IsNullOrWhiteSpace(alignment))
        {
            if (!Enum.TryParse<DocumentTextAlignment>(alignment, ignoreCase: true, out var parsed))
            {
                return Compilation.Fail(
                    McpToolResults.ValidationFailed,
                    $"Alignment '{alignment}' is invalid; use left, center, right, or justify.");
            }

            parsedAlignment = parsed;
        }

        if (parsedAlignment is null
            && lineSpacing is null
            && spacingBefore is null
            && spacingAfter is null
            && leftIndent is null
            && rightIndent is null
            && firstLineIndent is null)
        {
            return Compilation.Fail(
                McpToolResults.ValidationFailed,
                "Nothing to change — pass at least one of alignment, lineSpacing, spacingBefore, spacingAfter, leftIndent, rightIndent, firstLineIndent.");
        }

        var patch = new DocumentParagraphPropertiesPatch
        {
            Alignment = parsedAlignment,
            LineSpacing = lineSpacing,
            SpacingBefore = spacingBefore,
            SpacingAfter = spacingAfter,
            LeftIndent = leftIndent,
            RightIndent = rightIndent,
            FirstLineIndent = firstLineIndent
        };

        return Compilation.Ok(new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = target.BlockId, TableCellId = target.TableCellId },
            AttributeName = "paragraphProperties",
            AttributeValueJson = JsonSerializer.Serialize(patch, DocumentEditorJson.Options)
        });
    }

    // ---------------------------------------------------------------- plain-text mapping

    private sealed record InsertionPoint(int InlineIndex, int LocalOffset);

    private sealed record DeleteSegment(int InlineIndex, int LocalStart, int LocalLength);

    private static InsertionPoint MapInsertionPoint(List<InlineContent> inlines, int offset)
    {
        var plainStart = 0;
        for (var index = 0; index < inlines.Count; index++)
        {
            if (inlines[index] is not TextRun run)
            {
                continue;
            }

            var plainEnd = plainStart + run.Text.Length;
            if (offset <= plainEnd)
            {
                return new InsertionPoint(index, offset - plainStart);
            }

            plainStart = plainEnd;
        }

        // No text run can host the offset — only reachable for offset 0 in a block without text
        // runs; EnsureTextRun in the applier appends a fresh run at this index.
        return new InsertionPoint(inlines.Count, 0);
    }

    private static List<DeleteSegment> MapDeleteSegments(List<InlineContent> inlines, int offset, int length)
    {
        var segments = new List<DeleteSegment>();
        var rangeEnd = offset + length;
        var plainStart = 0;
        for (var index = 0; index < inlines.Count; index++)
        {
            if (inlines[index] is not TextRun run)
            {
                continue;
            }

            var plainEnd = plainStart + run.Text.Length;
            var overlapStart = Math.Max(offset, plainStart);
            var overlapEnd = Math.Min(rangeEnd, plainEnd);
            if (overlapEnd > overlapStart)
            {
                segments.Add(new DeleteSegment(index, overlapStart - plainStart, overlapEnd - overlapStart));
            }

            plainStart = plainEnd;
        }

        return segments;
    }

    /// <summary>
    /// Converts a plain-text boundary (text runs only) into the applier's mark-range coordinate
    /// space, where token and note-reference inlines contribute their display text. Start
    /// boundaries bind to the run that begins at the position (excluding preceding non-text
    /// inlines); end boundaries bind to the run that ends there (excluding following ones).
    /// </summary>
    private static int MapPlainToApplierBoundary(List<InlineContent> inlines, int plainPosition, bool isEnd)
    {
        var plainStart = 0;
        var applierStart = 0;
        foreach (var inline in inlines)
        {
            if (inline is TextRun run)
            {
                var plainEnd = plainStart + run.Text.Length;
                var inRange = isEnd
                    ? plainPosition > plainStart && plainPosition <= plainEnd
                    : plainPosition >= plainStart && plainPosition < plainEnd;
                if (inRange || (isEnd && plainPosition == 0 && plainStart == 0))
                {
                    return applierStart + (plainPosition - plainStart);
                }

                plainStart = plainEnd;
                applierStart += run.Text.Length;
            }
            else
            {
                applierStart += ApplierTextLength(inline);
            }
        }

        return applierStart;
    }

    /// <summary>Mirrors the applier's GetInlineText length for non-text inlines.</summary>
    private static int ApplierTextLength(InlineContent inline)
    {
        return inline switch
        {
            TextRun run => run.Text.Length,
            TokenRun token => (string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName).Length,
            DocumentNoteReferenceRun note => (note.DisplayMarker ?? note.NoteId).Length,
            _ => 0
        };
    }

    private static string PlainTextOf(List<InlineContent>? inlines)
        => DocumentEditorSemanticCore.PlainTextOf(inlines);

    private static string CamelCase(InlineMarkType type)
    {
        var name = type.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

}
