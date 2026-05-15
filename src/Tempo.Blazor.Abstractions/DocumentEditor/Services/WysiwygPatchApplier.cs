namespace Tempo.Blazor.DocumentEditor.Services;

using Tempo.Blazor.DocumentEditor.Models;

/// <summary>Applies WYSIWYG patches to a <see cref="DocumentEditorDocument"/>.</summary>
public sealed class WysiwygPatchApplier
{
    /// <summary>Current supported protocol version.</summary>
    public const int SupportedProtocolVersion = 1;

    /// <summary>Applies a single patch to the document.</summary>
    /// <param name="document">The document to mutate.</param>
    /// <param name="patch">The patch to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown when the patch protocol version is unsupported.</exception>
    /// <exception cref="ArgumentException">Thrown when the patch targets a missing block or inline.</exception>
    public void ApplyPatch(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (patch is null)
        {
            throw new ArgumentNullException(nameof(patch));
        }

        if (patch.ProtocolVersion > SupportedProtocolVersion)
        {
            throw new InvalidOperationException(
                $"Patch protocol version {patch.ProtocolVersion} is not supported. Maximum supported version is {SupportedProtocolVersion}.");
        }

        if (patch.ProtocolVersion < SupportedProtocolVersion)
        {
            patch = UpgradePatch(patch);
        }

        switch (patch.Type)
        {
            case "InsertText":
                ApplyInsertText(document, patch);
                break;
            case "InsertInline":
                ApplyInsertInline(document, patch);
                break;
            case "DeleteRange":
                ApplyDeleteRange(document, patch);
                break;
            case "SetMarks":
                ApplySetMarks(document, patch);
                break;
            case "InsertBlock":
                ApplyInsertBlock(document, patch);
                break;
            case "UpdateBlock":
                ApplyUpdateBlock(document, patch);
                break;
            case "RemoveBlock":
                ApplyRemoveBlock(document, patch);
                break;
            case "InsertParagraph":
                ApplyInsertParagraph(document, patch);
                break;
            case "InsertLineBreak":
                ApplyInsertLineBreak(document, patch);
                break;
            case "DeleteContentBackward":
                ApplyDeleteContentBackward(document, patch);
                break;
            case "DeleteContentForward":
                ApplyDeleteContentForward(document, patch);
                break;
            case "ToggleMark":
                ApplyToggleMark(document, patch);
                break;
            default:
                throw new ArgumentException($"Unknown patch type: {patch.Type}", nameof(patch));
        }
    }

    private static void ApplyInsertText(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var text = patch.Data ?? string.Empty;
        textRun.Text = textRun.Text.Insert(Math.Clamp(offset, 0, textRun.Text.Length), text);
    }

    private static void ApplyInsertInline(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (patch.Inline is null)
        {
            return;
        }

        var (_, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        if (inlines is null)
        {
            return;
        }

        var inserted = CloneInline(patch.Inline);
        if (string.IsNullOrWhiteSpace(inserted.Id))
        {
            inserted.Id = Guid.NewGuid().ToString("N");
        }

        if (inlines.Count == 0)
        {
            inlines.Add(inserted);
            return;
        }

        var inlineIndex = -1;
        if (!string.IsNullOrWhiteSpace(patch.Selection?.AnchorInlineId))
        {
            inlineIndex = inlines.FindIndex(inline => inline.Id == patch.Selection.AnchorInlineId);
        }

        if (inlineIndex < 0)
        {
            inlineIndex = 0;
        }

        var selected = inlines[inlineIndex];
        if (selected is TextRun textRun)
        {
            var offset = Math.Clamp(patch.Selection?.AnchorOffset ?? textRun.Text.Length, 0, textRun.Text.Length);
            var replacement = new List<InlineContent>();
            if (offset > 0)
            {
                var before = (TextRun)CloneInline(textRun);
                before.Id = Guid.NewGuid().ToString("N");
                before.Text = textRun.Text[..offset];
                replacement.Add(before);
            }

            replacement.Add(inserted);

            if (offset < textRun.Text.Length)
            {
                var after = (TextRun)CloneInline(textRun);
                after.Id = Guid.NewGuid().ToString("N");
                after.Text = textRun.Text[offset..];
                replacement.Add(after);
            }

            inlines.RemoveAt(inlineIndex);
            inlines.InsertRange(inlineIndex, replacement);
            return;
        }

        var insertIndex = (patch.Selection?.AnchorOffset ?? GetInlineText(selected).Length) <= 0
            ? inlineIndex
            : inlineIndex + 1;
        inlines.Insert(Math.Clamp(insertIndex, 0, inlines.Count), inserted);
    }

    private static void ApplyDeleteRange(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var length = patch.DeleteLength;
        var start = Math.Clamp(offset, 0, textRun.Text.Length);
        var end = Math.Clamp(offset + length, start, textRun.Text.Length);

        if (end > start)
        {
            textRun.Text = textRun.Text.Remove(start, end - start);
        }
    }

    private static void ApplyDeleteContentBackward(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var length = string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length;
        var start = Math.Clamp(offset - length, 0, textRun.Text.Length);
        var end = Math.Clamp(offset, start, textRun.Text.Length);

        if (end > start)
        {
            textRun.Text = textRun.Text.Remove(start, end - start);
        }
    }

    private static void ApplyDeleteContentForward(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var length = string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length;
        var start = Math.Clamp(offset, 0, textRun.Text.Length);
        var end = Math.Clamp(offset + length, start, textRun.Text.Length);

        if (end > start)
        {
            textRun.Text = textRun.Text.Remove(start, end - start);
        }
    }

    private static void ApplySetMarks(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        if (inlines is null || inlines.Count == 0)
        {
            return;
        }

        var markType = ParseMarkType(patch.MarkType);
        var anchorOffset = patch.Selection?.AnchorOffset ?? 0;
        var focusOffset = patch.Selection?.FocusOffset ?? anchorOffset;
        var startOffset = Math.Min(anchorOffset, focusOffset);
        var endOffset = Math.Max(anchorOffset, focusOffset);

        if (patch.Selection?.IsCollapsed == true || startOffset == endOffset)
        {
            return;
        }

        var text = string.Concat(inlines.Select(GetInlineText));
        startOffset = Math.Clamp(startOffset, 0, text.Length);
        endOffset = Math.Clamp(endOffset, startOffset, text.Length);

        if (startOffset >= endOffset)
        {
            return;
        }

        ApplyMarksToInlines(inlines, markType, startOffset, endOffset, patch.Data);
    }

    private static void ApplyToggleMark(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        if (inlines is null || inlines.Count == 0)
        {
            return;
        }

        var markType = ParseMarkType(patch.MarkType);
        var anchorOffset = patch.Selection?.AnchorOffset ?? 0;
        var focusOffset = patch.Selection?.FocusOffset ?? anchorOffset;
        var startOffset = Math.Min(anchorOffset, focusOffset);
        var endOffset = Math.Max(anchorOffset, focusOffset);

        if (patch.Selection?.IsCollapsed == true || startOffset == endOffset)
        {
            return;
        }

        var text = string.Concat(inlines.Select(GetInlineText));
        startOffset = Math.Clamp(startOffset, 0, text.Length);
        endOffset = Math.Clamp(endOffset, startOffset, text.Length);

        if (startOffset >= endOffset)
        {
            return;
        }

        // Check if the range already has the mark; if so, remove it, otherwise add it.
        var rangeHasMark = RangeHasMark(inlines, markType, startOffset, endOffset);
        if (rangeHasMark)
        {
            RemoveMarksFromInlines(inlines, markType, startOffset, endOffset);
        }
        else
        {
            ApplyMarksToInlines(inlines, markType, startOffset, endOffset, patch.Data);
        }
    }

    private static void ApplyInsertBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var blockType = ParseBlockType(patch.BlockType);
        var block = patch.Block ?? CreateDefaultBlock(blockType, patch.HeadingLevel);
        block.Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id;
        SanitizeBlock(block);

        var anchorBlockId = patch.Selection?.AnchorBlockId;
        var targetIndex = document.Blocks.Count;

        if (!string.IsNullOrWhiteSpace(anchorBlockId))
        {
            var anchorIndex = document.Blocks.FindIndex(b => b.Id == anchorBlockId);
            if (anchorIndex >= 0)
            {
                targetIndex = anchorIndex + 1;
            }
        }

        block.Order = CalculateOrder(document.Blocks, targetIndex);
        document.Blocks.Insert(targetIndex, block);
        SyncFloatingImageAnchor(document, block, patch.Selection);
    }

    private static void ApplyUpdateBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (patch.Block is null)
        {
            return;
        }

        SanitizeBlock(patch.Block);

        var existing = document.Blocks.FirstOrDefault(b => b.Id == patch.Block.Id);
        if (existing is null)
        {
            return;
        }

        existing.Type = patch.Block.Type;
        existing.Content = patch.Block.Content;
        // Phase 13: preserve existing order when patch carries 0 (JS table updates).
        if (patch.Block.Order != 0)
        {
            existing.Order = patch.Block.Order;
        }
        existing.SectionId = patch.Block.SectionId;
        SyncFloatingImageAnchor(document, existing, patch.Selection);
    }

    private static void ApplyRemoveBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var blockId = patch.Selection?.AnchorBlockId;
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        var index = document.Blocks.FindIndex(b => b.Id == blockId);
        if (index >= 0)
        {
            var block = document.Blocks[index];
            document.Blocks.RemoveAt(index);
            RemoveFloatingImageAnchor(document, block.Id);
            return;
        }

        // Phase 13: block may be inside a table cell.
        var cellResult = FindCellContainingBlock(document, blockId);
        if (cellResult.Cell is not null)
        {
            cellResult.Cell.Blocks.RemoveAll(b => b.Id == blockId);
        }
    }

    private static void ApplyInsertParagraph(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var anchorBlockId = patch.Selection?.AnchorBlockId;
        var targetIndex = document.Blocks.Count;

        if (!string.IsNullOrWhiteSpace(anchorBlockId))
        {
            var anchorIndex = document.Blocks.FindIndex(b => b.Id == anchorBlockId);
            if (anchorIndex >= 0)
            {
                targetIndex = anchorIndex + 1;
            }
            else
            {
                // Phase 13: block may be inside a table cell.
                var cellResult = FindCellContainingBlock(document, anchorBlockId);
                if (cellResult.Cell is not null)
                {
                    var cellIndex = cellResult.Cell.Blocks.FindIndex(b => b.Id == anchorBlockId);
                    if (cellIndex >= 0)
                    {
                        var newBlock = new DocumentBlock
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Type = DocumentBlockType.Paragraph,
                            Order = CalculateOrder(cellResult.Cell.Blocks, cellIndex + 1),
                            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = patch.Data ?? string.Empty }] }
                        };
                        cellResult.Cell.Blocks.Insert(cellIndex + 1, newBlock);
                    }
                    return;
                }
            }
        }

        var block = new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Paragraph,
            Order = CalculateOrder(document.Blocks, targetIndex),
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = patch.Data ?? string.Empty }] }
        };

        document.Blocks.Insert(targetIndex, block);
    }

    private static void ApplyInsertLineBreak(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        offset = Math.Clamp(offset, 0, textRun.Text.Length);

        var before = textRun.Text[..offset];
        var after = textRun.Text[offset..];

        textRun.Text = before;

        var targetIndex = document.Blocks.FindIndex(b => b.Id == block?.Id);
        List<DocumentBlock>? targetList = null;
        if (targetIndex < 0)
        {
            // Phase 13: block may be inside a table cell.
            var cellResult = FindCellContainingBlock(document, block?.Id);
            if (cellResult.Cell is not null)
            {
                targetList = cellResult.Cell.Blocks;
                targetIndex = targetList.FindIndex(b => b.Id == block?.Id);
                if (targetIndex >= 0)
                {
                    targetIndex++;
                }
                else
                {
                    targetIndex = targetList.Count;
                }
            }
            else
            {
                targetIndex = document.Blocks.Count;
            }
        }
        else
        {
            targetIndex++;
        }

        var newBlock = new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = block?.Type ?? DocumentBlockType.Paragraph,
            Order = CalculateOrder(targetList ?? document.Blocks, targetIndex),
            Content = CloneBlockContent(block?.Content, after)
        };

        if (targetList is not null)
        {
            targetList.Insert(targetIndex, newBlock);
        }
        else
        {
            document.Blocks.Insert(targetIndex, newBlock);
        }
    }

    private static (DocumentBlock? Block, InlineContent? Inline) ResolveBlockAndInline(DocumentEditorDocument document, WysiwygSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return (null, null);
        }

        var block = FindBlockById(document, selection.AnchorBlockId);
        if (block is null)
        {
            return (null, null);
        }

        var inlines = GetEditableInlines(block.Content);
        if (inlines is null || inlines.Count == 0)
        {
            return (block, null);
        }

        InlineContent? inline = null;
        if (!string.IsNullOrWhiteSpace(selection.AnchorInlineId))
        {
            inline = inlines.FirstOrDefault(i => i.Id == selection.AnchorInlineId);
        }

        inline ??= inlines.FirstOrDefault();
        return (block, inline);
    }

    private static (DocumentBlock? Block, List<InlineContent>? Inlines) ResolveBlockAndInlines(DocumentEditorDocument document, WysiwygSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return (null, null);
        }

        var block = FindBlockById(document, selection.AnchorBlockId);
        if (block is null)
        {
            return (null, null);
        }

        var inlines = GetEditableInlines(block.Content);
        return (block, inlines);
    }

    /// <summary>Phase 13: Finds a block by id, including inside table cells.</summary>
    private static DocumentBlock? FindBlockById(DocumentEditorDocument document, string? blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        var block = document.Blocks.FirstOrDefault(b => b.Id == blockId);
        if (block is not null)
        {
            return block;
        }

        // Search inside table cells.
        foreach (var tableBlock in document.Blocks.Where(b => b.Content is TableBlockContent))
        {
            var tableContent = (TableBlockContent)tableBlock.Content;
            foreach (var row in tableContent.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    var nested = cell.Blocks.FirstOrDefault(b => b.Id == blockId);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Phase 13: Finds the table cell containing a block.</summary>
    private static (TableCellContent? Cell, TableRowContent? Row, TableBlockContent? Table) FindCellContainingBlock(DocumentEditorDocument document, string? blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return (null, null, null);
        }

        foreach (var tableBlock in document.Blocks.Where(b => b.Content is TableBlockContent))
        {
            var tableContent = (TableBlockContent)tableBlock.Content;
            foreach (var row in tableContent.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.Blocks.Any(b => b.Id == blockId))
                    {
                        return (cell, row, tableContent);
                    }
                }
            }
        }

        return (null, null, null);
    }

    private static List<InlineContent>? GetEditableInlines(DocumentBlockContent? content)
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

    private static string GetInlineText(InlineContent inline)
    {
        return inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private static InlineMarkType ParseMarkType(string? markType)
    {
        if (string.IsNullOrWhiteSpace(markType))
        {
            return InlineMarkType.Bold;
        }

        return Enum.TryParse<InlineMarkType>(markType, ignoreCase: true, out var result)
            ? result
            : InlineMarkType.Bold;
    }

    private static DocumentBlockType ParseBlockType(string? blockType)
    {
        if (string.IsNullOrWhiteSpace(blockType))
        {
            return DocumentBlockType.Paragraph;
        }

        return Enum.TryParse<DocumentBlockType>(blockType, ignoreCase: true, out var result)
            ? result
            : DocumentBlockType.Paragraph;
    }

    private static DocumentBlock CreateDefaultBlock(DocumentBlockType type, int? headingLevel)
    {
        return type switch
        {
            DocumentBlockType.Heading => new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent
                {
                    Level = headingLevel ?? 1,
                    Inlines = [new TextRun { Text = string.Empty }]
                }
            },
            DocumentBlockType.List => new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Content = new ListBlockContent { Inlines = [new TextRun { Text = string.Empty }] }
            },
            DocumentBlockType.Quote => new DocumentBlock
            {
                Type = DocumentBlockType.Quote,
                Content = new QuoteBlockContent { Inlines = [new TextRun { Text = string.Empty }] }
            },
            DocumentBlockType.Image => new DocumentBlock
            {
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent()
            },
            DocumentBlockType.PageBreak => new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Content = new PageBreakBlockContent()
            },
            _ => new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = string.Empty }] }
            }
        };
    }

    private static void SanitizeBlock(DocumentBlock block)
    {
        if (block.Content is ImageBlockContent image && image.Source == DocumentImageSource.Url && !IsSafeImageUrl(image.Url))
        {
            image.Url = null;
        }
    }

    private static void SyncFloatingImageAnchor(DocumentEditorDocument document, DocumentBlock block, WysiwygSelectionSnapshot? selection)
    {
        if (block.Content is not ImageBlockContent image)
        {
            return;
        }

        var anchor = FindFloatingImageAnchor(document, block.Id);
        if (image.FloatingLayout?.Inline == false)
        {
            document.Anchors ??= [];
            var anchorBlockId = ResolveFloatingAnchorBlockId(document, block.Id, selection, anchor);
            if (anchor is null)
            {
                anchor = new DocumentAnchor
                {
                    Type = DocumentAnchorType.FloatingObject,
                    ObjectBlockId = block.Id
                };
                document.Anchors.Add(anchor);
            }

            anchor.Type = DocumentAnchorType.FloatingObject;
            anchor.ObjectBlockId = block.Id;
            if (image.FloatingLayout.LockAnchor && !string.IsNullOrWhiteSpace(anchor.BlockId))
            {
                anchorBlockId = anchor.BlockId;
            }

            anchor.BlockId = anchorBlockId;
            anchor.FloatingLayout = image.FloatingLayout;
            return;
        }

        if (anchor is not null)
        {
            document.Anchors.Remove(anchor);
        }
    }

    private static DocumentAnchor? FindFloatingImageAnchor(DocumentEditorDocument document, string blockId)
    {
        return document.Anchors.FirstOrDefault(anchor =>
            anchor.Type == DocumentAnchorType.FloatingObject
            && (string.Equals(anchor.ObjectBlockId, blockId, StringComparison.Ordinal)
                || string.Equals(anchor.BlockId, blockId, StringComparison.Ordinal)));
    }

    private static void RemoveFloatingImageAnchor(DocumentEditorDocument document, string blockId)
    {
        var anchor = FindFloatingImageAnchor(document, blockId);
        if (anchor is not null)
        {
            document.Anchors.Remove(anchor);
        }
    }

    private static string ResolveFloatingAnchorBlockId(
        DocumentEditorDocument document,
        string imageBlockId,
        WysiwygSelectionSnapshot? selection,
        DocumentAnchor? existingAnchor)
    {
        var selectedBlockId = selection?.AnchorBlockId;
        if (!string.IsNullOrWhiteSpace(selectedBlockId)
            && !string.Equals(selectedBlockId, imageBlockId, StringComparison.Ordinal)
            && FindBlockById(document, selectedBlockId) is not null)
        {
            return selectedBlockId;
        }

        if (!string.IsNullOrWhiteSpace(existingAnchor?.BlockId)
            && !string.Equals(existingAnchor.BlockId, imageBlockId, StringComparison.Ordinal)
            && FindBlockById(document, existingAnchor.BlockId) is not null)
        {
            return existingAnchor.BlockId;
        }

        var imageIndex = document.Blocks.FindIndex(candidate => candidate.Id == imageBlockId);
        if (imageIndex > 0)
        {
            for (var i = imageIndex - 1; i >= 0; i--)
            {
                if (document.Blocks[i].Content is ParagraphBlockContent or HeadingBlockContent or ListBlockContent or QuoteBlockContent)
                {
                    return document.Blocks[i].Id;
                }
            }
        }

        return imageBlockId;
    }

    private static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (url.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static double CalculateOrder(List<DocumentBlock> blocks, int insertIndex)
    {
        if (blocks.Count == 0)
        {
            return 10;
        }

        if (insertIndex <= 0)
        {
            return blocks[0].Order - 10;
        }

        if (insertIndex >= blocks.Count)
        {
            return blocks[^1].Order + 10;
        }

        return (blocks[insertIndex - 1].Order + blocks[insertIndex].Order) / 2.0;
    }

    private static DocumentBlockContent CloneBlockContent(DocumentBlockContent? source, string textAfter)
    {
        return source switch
        {
            HeadingBlockContent heading => new HeadingBlockContent
            {
                Level = heading.Level,
                Inlines = [new TextRun { Text = textAfter }]
            },
            ListBlockContent => new ListBlockContent { Inlines = [new TextRun { Text = textAfter }] },
            QuoteBlockContent => new QuoteBlockContent { Inlines = [new TextRun { Text = textAfter }] },
            _ => new ParagraphBlockContent { Inlines = [new TextRun { Text = textAfter }] }
        };
    }

    private static void ApplyMarksToInlines(List<InlineContent> inlines, InlineMarkType markType, int startOffset, int endOffset, string? data)
    {
        var currentOffset = 0;
        var newInlines = new List<InlineContent>();

        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = currentOffset;
            var inlineEnd = currentOffset + text.Length;

            if (inlineEnd <= startOffset || inlineStart >= endOffset)
            {
                newInlines.Add(CloneInline(inline));
                currentOffset += text.Length;
                continue;
            }

            var rangeStart = Math.Max(startOffset, inlineStart) - inlineStart;
            var rangeEnd = Math.Min(endOffset, inlineEnd) - inlineStart;

            if (rangeStart > 0)
            {
                newInlines.Add(SplitInline(inline, 0, rangeStart));
            }

            var marked = SplitInline(inline, rangeStart, rangeEnd);
            if (!marked.Marks.Any(m => m.Type == markType))
            {
                var mark = new InlineMark { Type = markType };
                if (markType == InlineMarkType.Link && !string.IsNullOrWhiteSpace(data))
                {
                    mark.Link = new LinkMarkData { Href = data };
                }
                marked.Marks.Add(mark);
            }
            newInlines.Add(marked);

            if (rangeEnd < text.Length)
            {
                newInlines.Add(SplitInline(inline, rangeEnd, text.Length));
            }

            currentOffset += text.Length;
        }

        inlines.Clear();
        inlines.AddRange(MergeAdjacentTextRuns(newInlines));
    }

    private static void RemoveMarksFromInlines(List<InlineContent> inlines, InlineMarkType markType, int startOffset, int endOffset)
    {
        var currentOffset = 0;
        var newInlines = new List<InlineContent>();

        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = currentOffset;
            var inlineEnd = currentOffset + text.Length;

            if (inlineEnd <= startOffset || inlineStart >= endOffset)
            {
                newInlines.Add(CloneInline(inline));
                currentOffset += text.Length;
                continue;
            }

            var rangeStart = Math.Max(startOffset, inlineStart) - inlineStart;
            var rangeEnd = Math.Min(endOffset, inlineEnd) - inlineStart;

            if (rangeStart > 0)
            {
                newInlines.Add(SplitInline(inline, 0, rangeStart));
            }

            var unmarked = SplitInline(inline, rangeStart, rangeEnd);
            unmarked.Marks.RemoveAll(m => m.Type == markType);
            newInlines.Add(unmarked);

            if (rangeEnd < text.Length)
            {
                newInlines.Add(SplitInline(inline, rangeEnd, text.Length));
            }

            currentOffset += text.Length;
        }

        inlines.Clear();
        inlines.AddRange(MergeAdjacentTextRuns(newInlines));
    }

    private static bool RangeHasMark(List<InlineContent> inlines, InlineMarkType markType, int startOffset, int endOffset)
    {
        var currentOffset = 0;

        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = currentOffset;
            var inlineEnd = currentOffset + text.Length;

            if (inlineEnd > startOffset && inlineStart < endOffset)
            {
                if (inline.Marks.Any(m => m.Type == markType))
                {
                    return true;
                }
            }

            currentOffset += text.Length;
        }

        return false;
    }

    private static InlineContent CloneInline(InlineContent inline)
    {
        return inline switch
        {
            TextRun text => new TextRun
            {
                Id = text.Id,
                Text = text.Text,
                Marks = text.Marks.Select(m => new InlineMark
                {
                    Type = m.Type,
                    Link = m.Link is not null ? new LinkMarkData { Href = m.Link.Href, Title = m.Link.Title } : null,
                    CommentAnchor = m.CommentAnchor is not null ? new CommentAnchorMarkData { CommentId = m.CommentAnchor.CommentId, AnchorId = m.CommentAnchor.AnchorId } : null,
                    RevisionId = m.RevisionId,
                    Value = m.Value
                }).ToList()
            },
            TokenRun token => new TokenRun
            {
                Id = token.Id,
                Key = token.Key,
                DisplayName = token.DisplayName,
                TokenType = token.TokenType,
                TypeLabel = token.TypeLabel,
                ColorClass = token.ColorClass,
                Description = token.Description,
                FallbackText = token.FallbackText,
                Marks = token.Marks.ToList()
            },
            DocumentNoteReferenceRun note => new DocumentNoteReferenceRun
            {
                Id = note.Id,
                NoteId = note.NoteId,
                NoteType = note.NoteType,
                DisplayMarker = note.DisplayMarker,
                Marks = note.Marks.ToList()
            },
            _ => new TextRun { Text = GetInlineText(inline) }
        };
    }

    private static InlineContent SplitInline(InlineContent inline, int start, int end)
    {
        var text = GetInlineText(inline);
        var length = Math.Min(end, text.Length) - start;
        length = Math.Max(length, 0);
        var slice = length > 0 ? text.Substring(start, length) : string.Empty;

        var cloned = CloneInline(inline);
        switch (cloned)
        {
            case TextRun textRun:
                textRun.Text = slice;
                break;
            case TokenRun tokenRun:
                tokenRun.Key = slice;
                break;
            case DocumentNoteReferenceRun noteRun:
                noteRun.NoteId = slice;
                break;
        }

        return cloned;
    }

    private static List<InlineContent> MergeAdjacentTextRuns(List<InlineContent> inlines)
    {
        if (inlines.Count < 2)
        {
            return inlines;
        }

        var merged = new List<InlineContent>();
        InlineContent? previous = null;

        foreach (var inline in inlines)
        {
            if (previous is TextRun prevText && inline is TextRun currText
                && MarksAreEqual(prevText.Marks, currText.Marks))
            {
                prevText.Text += currText.Text;
            }
            else
            {
                if (previous is not null)
                {
                    merged.Add(previous);
                }
                previous = inline;
            }
        }

        if (previous is not null)
        {
            merged.Add(previous);
        }

        return merged;
    }

    private static bool MarksAreEqual(List<InlineMark> a, List<InlineMark> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        var aTypes = a.Select(m => m.Type).OrderBy(t => t).ToList();
        var bTypes = b.Select(m => m.Type).OrderBy(t => t).ToList();

        return aTypes.SequenceEqual(bTypes);
    }

    private static WysiwygPatch UpgradePatch(WysiwygPatch patch)
    {
        // Graceful upgrade: return a copy with the current protocol version.
        // Concrete upgrade logic will be added when future protocol versions are introduced.
        return new WysiwygPatch
        {
            Type = patch.Type,
            Data = patch.Data,
            Selection = patch.Selection,
            TransactionId = patch.TransactionId,
            ProtocolVersion = SupportedProtocolVersion,
            Html = patch.Html,
            Plain = patch.Plain,
            HasImage = patch.HasImage,
            MarkType = patch.MarkType,
            BlockType = patch.BlockType,
            Block = patch.Block,
            DeleteLength = patch.DeleteLength,
            HeadingLevel = patch.HeadingLevel
        };
    }
}
