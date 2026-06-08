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
            case "SplitBlock":
                ApplySplitBlock(document, patch);
                break;
            case "UpdateBlock":
                ApplyUpdateBlock(document, patch);
                break;
            case "MoveBlock":
                ApplyMoveBlock(document, patch);
                break;
            case "RemoveBlock":
                ApplyRemoveBlock(document, patch);
                break;
            case "InsertParagraph":
                ApplyInsertParagraph(document, patch);
                break;
            case "InsertLineBreak":
            case "InsertSoftBreak":
                ApplyInsertSoftBreak(document, patch);
                break;
            case "MergeWithPreviousBlock":
                ApplyMergeWithPreviousBlock(document, patch);
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
            case "ClearFormatting":
                ApplyClearFormatting(document, patch);
                break;
            case "SetParagraphProperties":
                ApplySetParagraphProperties(document, patch);
                break;
            default:
                throw new ArgumentException($"Unknown patch type: {patch.Type}", nameof(patch));
        }
    }

    private static void ApplyInsertText(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (_, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        var inlineIndex = inlines is null ? -1 : FindInlineIndex(inlines, patch.Selection?.AnchorInlineId);
        var inline = inlineIndex >= 0 ? inlines![inlineIndex] : ResolveBlockAndInline(document, patch.Selection).Inline;
        var textRun = inline as TextRun ?? EnsureTableCellTextRun(document, patch.Selection);
        if (textRun is null)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var text = patch.Data ?? string.Empty;
        if (inlines is not null
            && inlineIndex >= 0
            && string.IsNullOrWhiteSpace(patch.RevisionId)
            && string.IsNullOrWhiteSpace(patch.RevisionType)
            && textRun.Marks.Any(mark => mark.Type == InlineMarkType.Revision))
        {
            var insertedInlineId = ResolveInsertedInlineId(patch, textRun.Id);
            InsertUntrackedTextOutsideRevision(inlines, inlineIndex, textRun, offset, text, insertedInlineId);
            return;
        }

        textRun.Text = textRun.Text.Insert(Math.Clamp(offset, 0, textRun.Text.Length), text);
    }

    private static void InsertUntrackedTextOutsideRevision(
        List<InlineContent> inlines,
        int inlineIndex,
        TextRun revisionRun,
        int offset,
        string text,
        string insertedInlineId)
    {
        var clampedOffset = Math.Clamp(offset, 0, revisionRun.Text.Length);
        var replacement = new List<InlineContent>();
        AddTextRunSlice(replacement, revisionRun, 0, clampedOffset);
        replacement.Add(new TextRun
        {
            Id = insertedInlineId,
            Text = text,
            Marks = CloneTypingMarks(revisionRun.Marks)
        });
        AddTextRunSlice(replacement, revisionRun, clampedOffset, revisionRun.Text.Length);

        inlines.RemoveAt(inlineIndex);
        inlines.InsertRange(inlineIndex, MergeAdjacentTextRuns(replacement));
    }

    private static string ResolveInsertedInlineId(WysiwygPatch patch, string? sourceInlineId)
    {
        var candidate = patch.AfterSelection?.AnchorInlineId
            ?? patch.AfterSelection?.FocusInlineId
            ?? patch.Inline?.Id;
        if (string.IsNullOrWhiteSpace(candidate) || string.Equals(candidate, sourceInlineId, StringComparison.Ordinal))
        {
            return Guid.NewGuid().ToString("N");
        }

        return candidate;
    }

    private static void AddTextRunSlice(List<InlineContent> target, TextRun source, int start, int end)
    {
        var safeStart = Math.Clamp(start, 0, source.Text.Length);
        var safeEnd = Math.Clamp(end, safeStart, source.Text.Length);
        if (safeEnd <= safeStart)
        {
            return;
        }

        target.Add(new TextRun
        {
            Id = safeStart == 0 ? source.Id : Guid.NewGuid().ToString("N"),
            Text = source.Text[safeStart..safeEnd],
            Marks = source.Marks.Select(CloneMark).ToList()
        });
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
        var (_, inline) = ResolveBlockAndInline(document, patch.Selection);
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
        var (_, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        var length = string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length;
        if (offset <= 0)
        {
            ApplyMergeWithPreviousBlock(document, patch);
            return;
        }

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
        if (offset >= textRun.Text.Length && string.IsNullOrEmpty(patch.Data))
        {
            ApplyMergeWithNextBlock(document, patch);
            return;
        }

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
        var (anchorOffset, focusOffset) = ResolveFormattingRangeOffsets(patch.Selection);
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

        ApplyMarksToInlines(inlines, markType, startOffset, endOffset, patch.Data, patch.LinkTitle);
    }

    private static void ApplyToggleMark(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (block, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        if (inlines is null || inlines.Count == 0)
        {
            return;
        }

        var markType = ParseMarkType(patch.MarkType);
        var (anchorOffset, focusOffset) = ResolveFormattingRangeOffsets(patch.Selection);
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
            ApplyMarksToInlines(inlines, markType, startOffset, endOffset, patch.Data, patch.LinkTitle);
        }
    }

    private static void ApplyClearFormatting(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (_, inlines) = ResolveBlockAndInlines(document, patch.Selection);
        if (inlines is null || inlines.Count == 0)
        {
            return;
        }

        var (anchorOffset, focusOffset) = ResolveFormattingRangeOffsets(patch.Selection);
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

        RemoveStyleMarksFromInlines(inlines, startOffset, endOffset);
    }

    private static void ApplyInsertBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var blockType = ParseBlockType(patch.BlockType);
        var block = patch.Block ?? CreateDefaultBlock(blockType, patch.HeadingLevel);
        block.Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id;
        SanitizeBlock(block);

        if (IsStructuralBlockSplit(patch) && TryApplyStructuralBlockSplit(document, patch, block))
        {
            return;
        }

        if (TryFindBlockList(document, block.Id, out var existingBlocks, out var existingIndex, patch.Selection))
        {
            UpdateExistingBlock(document, existingBlocks[existingIndex], block, patch.Selection);
            return;
        }

        var targetBlocks = ResolveTopLevelBlockList(document, patch.Selection);
        var anchorBlockId = patch.Selection?.AnchorBlockId;
        var targetIndex = targetBlocks.Count;

        if (!string.IsNullOrWhiteSpace(anchorBlockId))
        {
            var anchorIndex = targetBlocks.FindIndex(b => b.Id == anchorBlockId);
            if (anchorIndex >= 0)
            {
                targetIndex = anchorIndex + 1;
            }
        }

        block.Order = CalculateOrder(targetBlocks, targetIndex);
        targetBlocks.Insert(targetIndex, block);
        SyncFloatingImageAnchor(document, block, patch.Selection);
    }

    private static bool IsStructuralBlockSplit(WysiwygPatch patch)
        => string.Equals(patch.RevisionType, "Structural", StringComparison.Ordinal)
            && string.Equals(patch.BlockType, "Paragraph", StringComparison.OrdinalIgnoreCase)
            && patch.Selection is not null;

    private static bool TryApplyStructuralBlockSplit(DocumentEditorDocument document, WysiwygPatch patch, DocumentBlock block)
    {
        return TryApplyBlockSplit(document, patch, block);
    }

    private static bool TryFindBlockList(
        DocumentEditorDocument document,
        string? blockId,
        out List<DocumentBlock> blocks,
        out int index,
        WysiwygSelectionSnapshot? selection = null)
    {
        if (selection is not null
            && IsHeaderFooterRegion(selection)
            && !string.IsNullOrWhiteSpace(selection.HeaderFooterId)
            && DocumentHeaderFooterResolver.FindById(document, selection.HeaderFooterId) is { } headerFooter)
        {
            blocks = headerFooter.Blocks;
            index = string.IsNullOrWhiteSpace(blockId)
                ? -1
                : blocks.FindIndex(block => block.Id == blockId);
            if (index >= 0 || string.IsNullOrWhiteSpace(blockId))
            {
                return index >= 0;
            }
        }

        blocks = document.Blocks;
        index = string.IsNullOrWhiteSpace(blockId)
            ? -1
            : document.Blocks.FindIndex(block => block.Id == blockId);
        if (index >= 0)
        {
            return true;
        }

        foreach (var candidateHeaderFooter in document.HeadersFooters)
        {
            blocks = candidateHeaderFooter.Blocks;
            index = string.IsNullOrWhiteSpace(blockId)
                ? -1
                : blocks.FindIndex(block => block.Id == blockId);
            if (index >= 0)
            {
                return true;
            }
        }

        var cellResult = FindCellContainingBlock(document, blockId, selection);
        if (cellResult.Cell is not null)
        {
            blocks = cellResult.Cell.Blocks;
            index = blocks.FindIndex(block => block.Id == blockId);
            return index >= 0;
        }

        return false;
    }

    private static int FindInlineIndex(List<InlineContent> inlines, string? inlineId)
    {
        if (string.IsNullOrWhiteSpace(inlineId))
        {
            return 0;
        }

        var index = inlines.FindIndex(inline => inline.Id == inlineId);
        if (index >= 0)
        {
            return index;
        }

        const string revisionInlinePrefix = "rev-";
        if (inlineId.StartsWith(revisionInlinePrefix, StringComparison.Ordinal))
        {
            var revisionId = inlineId[revisionInlinePrefix.Length..];
            return inlines.FindIndex(inline => inline.Marks.Any(mark =>
                mark.Type == InlineMarkType.Revision
                && string.Equals(mark.RevisionId, revisionId, StringComparison.Ordinal)));
        }

        return -1;
    }

    private static string? GetFirstEditableInlineId(DocumentBlockContent? content)
        => GetEditableInlines(content)?.FirstOrDefault()?.Id;

    private static void ApplyUpdateBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (patch.Block is null)
        {
            return;
        }

        SanitizeBlock(patch.Block);

        if (!TryFindBlockList(document, patch.Block.Id, out var blocks, out var existingIndex, patch.Selection))
        {
            var targetBlocks = ResolveTopLevelBlockList(document, patch.Selection);
            var targetIndex = targetBlocks.Count;
            patch.Block.Order = patch.Block.Order == 0
                ? CalculateOrder(targetBlocks, targetIndex)
                : patch.Block.Order;
            targetBlocks.Insert(targetIndex, patch.Block);
            SyncFloatingImageAnchor(document, patch.Block, patch.Selection);
            return;
        }

        UpdateExistingBlock(document, blocks[existingIndex], patch.Block, patch.Selection);
    }

    private static void UpdateExistingBlock(
        DocumentEditorDocument document,
        DocumentBlock existing,
        DocumentBlock updated,
        WysiwygSelectionSnapshot? selection)
    {
        existing.Type = updated.Type;
        existing.Content = updated.Content;
        existing.ParagraphProperties = updated.ParagraphProperties ?? new DocumentParagraphProperties();
        // Phase 13: preserve existing order when patch carries 0 (JS table updates).
        if (updated.Order != 0)
        {
            existing.Order = updated.Order;
        }

        existing.SectionId = updated.SectionId;
        SyncFloatingImageAnchor(document, existing, selection);
    }

    private static void ApplySetParagraphProperties(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (patch.ParagraphProperties is null)
        {
            return;
        }

        foreach (var block in ResolveSelectedBlocks(document, patch.Selection))
        {
            if (!IsParagraphLikeBlock(block))
            {
                continue;
            }

            block.ParagraphProperties ??= new DocumentParagraphProperties();
            ApplyParagraphPropertiesPatch(block.ParagraphProperties, patch.ParagraphProperties);
        }
    }

    private static IEnumerable<DocumentBlock> ResolveSelectedBlocks(DocumentEditorDocument document, WysiwygSelectionSnapshot? selection)
    {
        if (selection is null || string.IsNullOrWhiteSpace(selection.AnchorBlockId))
        {
            return [];
        }

        if (!TryFindBlockList(document, selection.AnchorBlockId, out var anchorBlocks, out var anchorIndex, selection))
        {
            return [];
        }

        var focusBlockId = string.IsNullOrWhiteSpace(selection.FocusBlockId)
            ? selection.AnchorBlockId
            : selection.FocusBlockId;
        var focusIndex = anchorBlocks.FindIndex(block => block.Id == focusBlockId);
        if (focusIndex < 0)
        {
            focusIndex = anchorIndex;
        }

        var start = Math.Min(anchorIndex, focusIndex);
        var end = Math.Max(anchorIndex, focusIndex);
        return anchorBlocks.Skip(start).Take(end - start + 1).ToList();
    }

    private static bool IsParagraphLikeBlock(DocumentBlock block)
    {
        return block.Content is ParagraphBlockContent
            or HeadingBlockContent
            or ListBlockContent
            or QuoteBlockContent;
    }

    private static void ApplyParagraphPropertiesPatch(
        DocumentParagraphProperties properties,
        DocumentParagraphPropertiesPatch patch)
    {
        if (patch.Alignment is { } alignment)
        {
            properties.Alignment = alignment;
        }

        if (patch.LineSpacing is { } lineSpacing)
        {
            properties.LineSpacing = Math.Clamp(lineSpacing, 0.8, 3);
        }

        if (patch.SpacingBefore is { } spacingBefore)
        {
            properties.SpacingBefore = Math.Clamp(spacingBefore, 0, 144);
        }

        if (patch.SpacingAfter is { } spacingAfter)
        {
            properties.SpacingAfter = Math.Clamp(spacingAfter, 0, 144);
        }

        if (patch.LeftIndent is { } leftIndent)
        {
            properties.LeftIndent = Math.Clamp(leftIndent, 0, 432);
        }

        if (patch.RightIndent is { } rightIndent)
        {
            properties.RightIndent = Math.Clamp(rightIndent, 0, 432);
        }

        if (patch.FirstLineIndent is { } firstLineIndent)
        {
            properties.FirstLineIndent = Math.Clamp(firstLineIndent, -216, 216);
        }

        if (patch.LeftIndentDelta is { } leftIndentDelta)
        {
            properties.LeftIndent = Math.Clamp(properties.LeftIndent + leftIndentDelta, 0, 432);
        }

        if (patch.RightIndentDelta is { } rightIndentDelta)
        {
            properties.RightIndent = Math.Clamp(properties.RightIndent + rightIndentDelta, 0, 432);
        }

        if (patch.FirstLineIndentDelta is { } firstLineIndentDelta)
        {
            properties.FirstLineIndent = Math.Clamp(properties.FirstLineIndent + firstLineIndentDelta, -216, 216);
        }
    }

    private static void ApplyMoveBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var blockId = patch.Block?.Id ?? patch.Selection?.AnchorBlockId;
        if (string.IsNullOrWhiteSpace(blockId) || patch.Block?.Order is null or 0)
        {
            return;
        }

        if (!TryFindBlockList(document, blockId, out var blocks, out var index, patch.Selection))
        {
            return;
        }

        var block = blocks[index];
        block.Order = patch.Block.Order;
        blocks.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    private static void ApplyRemoveBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var blockId = patch.Selection?.AnchorBlockId;
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        if (TryFindBlockList(document, blockId, out var blocks, out var index, patch.Selection))
        {
            var block = blocks[index];
            var fallbackAnchorBlockId = FindFallbackObjectAnchorBlockId(blocks, index);
            blocks.RemoveAt(index);
            RemoveFloatingImageAnchor(document, block.Id);
            RepairFloatingImageAnchorsAfterBlockRemoved(document, block.Id, fallbackAnchorBlockId, patch.Selection);
            return;
        }

        // Phase 13: block may be inside a table cell.
        var cellResult = FindCellContainingBlock(document, blockId, patch.Selection);
        if (cellResult.Cell is not null)
        {
            var cellIndex = cellResult.Cell.Blocks.FindIndex(b => b.Id == blockId);
            var fallbackAnchorBlockId = cellIndex >= 0
                ? FindFallbackObjectAnchorBlockId(cellResult.Cell.Blocks, cellIndex)
                : null;
            cellResult.Cell.Blocks.RemoveAll(b => b.Id == blockId);
            RemoveFloatingImageAnchor(document, blockId);
            RepairFloatingImageAnchorsAfterBlockRemoved(document, blockId, fallbackAnchorBlockId, patch.Selection);
        }
    }

    private static void ApplyInsertParagraph(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (!string.IsNullOrWhiteSpace(patch.Selection?.AnchorInlineId))
        {
            ApplySplitBlock(document, patch);
            return;
        }

        var targetBlocks = ResolveTopLevelBlockList(document, patch.Selection);
        var anchorBlockId = patch.Selection?.AnchorBlockId;
        var targetIndex = targetBlocks.Count;

        if (!string.IsNullOrWhiteSpace(anchorBlockId))
        {
            var anchorIndex = targetBlocks.FindIndex(b => b.Id == anchorBlockId);
            if (anchorIndex >= 0)
            {
                targetIndex = anchorIndex + 1;
            }
            else
            {
                // Phase 13: block may be inside a table cell.
                var cellResult = FindCellContainingBlock(document, anchorBlockId, patch.Selection);
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
            Order = CalculateOrder(targetBlocks, targetIndex),
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = patch.Data ?? string.Empty }] }
        };

        targetBlocks.Insert(targetIndex, block);
    }

    private static void ApplySplitBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var block = patch.Block ?? CreateDefaultBlock(DocumentBlockType.Paragraph, patch.HeadingLevel);
        block.Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id;
        SanitizeBlock(block);

        if (!TryApplyBlockSplit(document, patch, block))
        {
            ApplyInsertParagraph(document, patch);
        }
    }

    private static bool TryApplyBlockSplit(DocumentEditorDocument document, WysiwygPatch patch, DocumentBlock block)
    {
        if (!TryFindBlockList(document, patch.Selection?.AnchorBlockId, out var blocks, out var anchorIndex, patch.Selection))
        {
            return false;
        }

        var anchorBlock = blocks[anchorIndex];
        var inlines = GetEditableInlines(anchorBlock.Content);
        if (inlines is null)
        {
            return false;
        }

        if (inlines.Count == 0)
        {
            inlines.Add(new TextRun
            {
                Id = patch.Selection?.AnchorInlineId ?? Guid.NewGuid().ToString("N"),
                Text = string.Empty
            });
        }

        var inlineIndex = FindInlineIndex(inlines, patch.Selection?.AnchorInlineId);
        if (inlineIndex < 0)
        {
            inlineIndex = Math.Clamp(inlines.Count - 1, 0, inlines.Count - 1);
        }

        var newInlineId = GetFirstEditableInlineId(block.Content) ?? patch.AfterSelection?.AnchorInlineId ?? Guid.NewGuid().ToString("N");
        var split = SplitInlinesForBlockBreak(inlines, inlineIndex, patch.Selection?.AnchorOffset ?? 0, newInlineId);

        inlines.Clear();
        inlines.AddRange(split.Before);

        block.Content = block.Content switch
        {
            ListBlockContent list => new ListBlockContent
            {
                Ordered = list.Ordered,
                IndentLevel = list.IndentLevel,
                StartNumber = Math.Max(1, list.StartNumber),
                Inlines = split.After
            },
            HeadingBlockContent heading => new HeadingBlockContent
            {
                Level = Math.Clamp(heading.Level, 1, 6),
                Inlines = split.After
            },
            QuoteBlockContent => new QuoteBlockContent { Inlines = split.After },
            _ => new ParagraphBlockContent { Inlines = split.After }
        };
        var targetIndex = anchorIndex + 1;
        block.Order = CalculateOrder(blocks, targetIndex);
        blocks.Insert(targetIndex, block);
        SyncFloatingImageAnchor(document, block, patch.Selection);
        return true;
    }

    private static (List<InlineContent> Before, List<InlineContent> After) SplitInlinesForBlockBreak(
        List<InlineContent> inlines,
        int inlineIndex,
        int offset,
        string newInlineId)
    {
        var before = new List<InlineContent>();
        var after = new List<InlineContent>();
        var selected = inlines[inlineIndex];
        var typingMarks = CloneTypingMarks(selected.Marks);

        for (var i = 0; i < inlines.Count; i++)
        {
            var inline = inlines[i];
            if (i < inlineIndex)
            {
                before.Add(CloneInline(inline));
                continue;
            }

            if (i > inlineIndex)
            {
                after.Add(CloneInline(inline));
                continue;
            }

            if (inline is TextRun textRun)
            {
                var splitOffset = Math.Clamp(offset, 0, textRun.Text.Length);
                var beforeText = textRun.Text[..splitOffset];
                var afterText = textRun.Text[splitOffset..];

                if (beforeText.Length > 0)
                {
                    var beforeRun = (TextRun)CloneInline(textRun);
                    beforeRun.Text = beforeText;
                    before.Add(beforeRun);
                }

                if (afterText.Length > 0)
                {
                    var afterRun = (TextRun)CloneInline(textRun);
                    afterRun.Id = newInlineId;
                    afterRun.Text = afterText;
                    after.Add(afterRun);
                }

                continue;
            }

            var selectedTextLength = GetInlineText(inline).Length;
            if (offset <= 0)
            {
                var moved = CloneInline(inline);
                moved.Id = newInlineId;
                after.Add(moved);
            }
            else if (offset >= selectedTextLength)
            {
                before.Add(CloneInline(inline));
            }
        }

        if (before.Count == 0)
        {
            before.Add(new TextRun
            {
                Id = selected.Id,
                Text = string.Empty,
                Marks = CloneTypingMarks(selected.Marks)
            });
        }

        if (after.Count == 0)
        {
            after.Add(new TextRun
            {
                Id = newInlineId,
                Text = string.Empty,
                Marks = typingMarks
            });
        }

        return (before, after);
    }

    private static void ApplyInsertSoftBreak(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var (_, inline) = ResolveBlockAndInline(document, patch.Selection);
        if (inline is not TextRun textRun)
        {
            return;
        }

        var offset = patch.Selection?.AnchorOffset ?? 0;
        offset = Math.Clamp(offset, 0, textRun.Text.Length);
        textRun.Text = textRun.Text.Insert(offset, "\n");
    }

    private static void ApplyMergeWithPreviousBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (!TryFindBlockList(document, patch.Selection?.AnchorBlockId, out var blocks, out var index, patch.Selection) || index <= 0)
        {
            return;
        }

        var current = blocks[index];
        var previous = blocks[index - 1];
        var currentInlines = GetEditableInlines(current.Content);
        var previousInlines = GetEditableInlines(previous.Content);
        if (currentInlines is null || previousInlines is null)
        {
            return;
        }

        if (previousInlines.Count == 1 && previousInlines[0] is TextRun { Text.Length: 0 })
        {
            previousInlines.Clear();
        }

        previousInlines.AddRange(currentInlines.Select(CloneInline));
        blocks.RemoveAt(index);
        RemoveFloatingImageAnchor(document, current.Id);
    }

    private static void ApplyMergeWithNextBlock(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (!TryFindBlockList(document, patch.Selection?.AnchorBlockId, out var blocks, out var index, patch.Selection)
            || index < 0
            || index >= blocks.Count - 1)
        {
            return;
        }

        var current = blocks[index];
        var next = blocks[index + 1];
        var currentInlines = GetEditableInlines(current.Content);
        var nextInlines = GetEditableInlines(next.Content);
        if (currentInlines is null || nextInlines is null)
        {
            return;
        }

        if (currentInlines.Count == 1 && currentInlines[0] is TextRun { Text.Length: 0 })
        {
            currentInlines.Clear();
        }

        currentInlines.AddRange(nextInlines.Select(CloneInline));
        blocks.RemoveAt(index + 1);
        RemoveFloatingImageAnchor(document, next.Id);
    }

    private static (DocumentBlock? Block, InlineContent? Inline) ResolveBlockAndInline(DocumentEditorDocument document, WysiwygSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return (null, null);
        }

        var block = FindBlockById(document, selection.AnchorBlockId, selection);
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

        var block = FindBlockById(document, selection.AnchorBlockId, selection);
        if (block is null)
        {
            return (null, null);
        }

        var inlines = GetEditableInlines(block.Content);
        return (block, inlines);
    }

    /// <summary>Phase 13: Finds a block by id, including inside table cells.</summary>
    private static DocumentBlock? FindBlockById(
        DocumentEditorDocument document,
        string? blockId,
        WysiwygSelectionSnapshot? selection = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        if (TryFindBlockList(document, blockId, out var blocks, out var index, selection))
        {
            return blocks[index];
        }

        return null;
    }

    private static List<DocumentBlock> ResolveTopLevelBlockList(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot? selection)
    {
        if (selection is not null
            && string.Equals(selection.Region, "TableCell", StringComparison.OrdinalIgnoreCase)
            && FindTableCellById(document, selection.ActiveTableCellId, selection: null) is { } selectedCell)
        {
            return selectedCell.Blocks;
        }

        if (selection is not null
            && IsHeaderFooterRegion(selection)
            && !string.IsNullOrWhiteSpace(selection.HeaderFooterId)
            && DocumentHeaderFooterResolver.FindById(document, selection.HeaderFooterId) is { } headerFooter)
        {
            return headerFooter.Blocks;
        }

        return document.Blocks;
    }

    private static bool IsHeaderFooterRegion(WysiwygSelectionSnapshot selection)
        => string.Equals(selection.Region, "Header", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selection.Region, "Footer", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<List<DocumentBlock>> EnumerateTopLevelBlockLists(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot? selection = null)
    {
        var preferred = ResolveTopLevelBlockList(document, selection);
        yield return preferred;

        if (!ReferenceEquals(preferred, document.Blocks))
        {
            yield return document.Blocks;
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            if (!ReferenceEquals(preferred, headerFooter.Blocks))
            {
                yield return headerFooter.Blocks;
            }
        }
    }

    private static IEnumerable<DocumentBlock> EnumerateTopLevelBlocks(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot? selection = null)
    {
        foreach (var blocks in EnumerateTopLevelBlockLists(document, selection))
        {
            foreach (var block in blocks)
            {
                yield return block;
            }
        }
    }

    /// <summary>Phase 13: Finds the table cell containing a block.</summary>
    private static (TableCellContent? Cell, TableRowContent? Row, TableBlockContent? Table) FindCellContainingBlock(
        DocumentEditorDocument document,
        string? blockId,
        WysiwygSelectionSnapshot? selection = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return (null, null, null);
        }

        foreach (var tableBlock in EnumerateTopLevelBlocks(document, selection).Where(b => b.Content is TableBlockContent))
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

    private static TableCellContent? FindTableCellById(
        DocumentEditorDocument document,
        string? cellId,
        WysiwygSelectionSnapshot? selection = null)
    {
        if (string.IsNullOrWhiteSpace(cellId))
        {
            return null;
        }

        foreach (var tableBlock in EnumerateTopLevelBlocks(document, selection).Where(b => b.Content is TableBlockContent))
        {
            var tableContent = (TableBlockContent)tableBlock.Content;
            foreach (var row in tableContent.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.Id == cellId)
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    private static TextRun? EnsureTableCellTextRun(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot? selection)
    {
        if (selection is null || !string.Equals(selection.Region, "TableCell", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cell = FindTableCellById(document, selection.ActiveTableCellId, selection);
        if (cell is null)
        {
            return null;
        }

        var block = !string.IsNullOrWhiteSpace(selection.AnchorBlockId)
            ? cell.Blocks.FirstOrDefault(b => b.Id == selection.AnchorBlockId)
            : null;
        if (block is null)
        {
            block = cell.Blocks.FirstOrDefault(b => GetEditableInlines(b.Content) is not null);
        }

        if (block is null)
        {
            block = new DocumentBlock
            {
                Id = string.IsNullOrWhiteSpace(selection.AnchorBlockId) ? Guid.NewGuid().ToString("N") : selection.AnchorBlockId!,
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent()
            };
            cell.Blocks.Add(block);
        }

        var inlines = GetEditableInlines(block.Content);
        if (inlines is null)
        {
            block.Type = DocumentBlockType.Paragraph;
            block.Content = new ParagraphBlockContent();
            inlines = ((ParagraphBlockContent)block.Content).Inlines;
        }

        var run = !string.IsNullOrWhiteSpace(selection.AnchorInlineId)
            ? inlines.OfType<TextRun>().FirstOrDefault(i => i.Id == selection.AnchorInlineId)
            : null;
        if (run is not null)
        {
            return run;
        }

        run = inlines.OfType<TextRun>().FirstOrDefault();
        if (run is not null)
        {
            return run;
        }

        run = new TextRun
        {
            Id = string.IsNullOrWhiteSpace(selection.AnchorInlineId) ? Guid.NewGuid().ToString("N") : selection.AnchorInlineId!,
            Text = string.Empty
        };
        inlines.Add(run);
        return run;
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
            DocumentFieldRun field => ResolveFieldDisplayText(field),
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private static string ResolveFieldDisplayText(DocumentFieldRun field)
    {
        if (!string.IsNullOrWhiteSpace(field.DisplayText))
        {
            return field.DisplayText;
        }

        if (!string.IsNullOrWhiteSpace(field.FallbackText))
        {
            return field.FallbackText;
        }

        return field.FieldType switch
        {
            DocumentFieldType.PageNumber => "1",
            DocumentFieldType.PageCount => "1",
            DocumentFieldType.PageXOfY => "1 / 1",
            DocumentFieldType.Date => DateTime.Today.ToShortDateString(),
            DocumentFieldType.DocumentTitle => "Document title",
            DocumentFieldType.Author => "Author",
            DocumentFieldType.LastSaved => DateTime.Today.ToShortDateString(),
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

    private static (int AnchorOffset, int FocusOffset) ResolveFormattingRangeOffsets(WysiwygSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return (0, 0);
        }

        var anchorOffset = selection.AnchorOffset;
        var focusOffset = selection.FocusOffset;
        var sameBlock = string.Equals(selection.AnchorBlockId, selection.FocusBlockId, StringComparison.Ordinal);
        var hasBlockOffsets = selection.AnchorBlockOffset != 0 || selection.FocusBlockOffset != 0;

        return sameBlock && hasBlockOffsets
            ? (selection.AnchorBlockOffset, selection.FocusBlockOffset)
            : (anchorOffset, focusOffset);
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
        DocumentImagePersistence.Sanitize(block);
    }

    private static void SyncFloatingImageAnchor(DocumentEditorDocument document, DocumentBlock block, WysiwygSelectionSnapshot? selection)
    {
        if (block.Content is not ImageBlockContent image)
        {
            return;
        }

        var anchor = FindFloatingImageAnchor(document, block.Id);
        if (!image.Layout.IsInline)
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
            if (image.Layout.Anchor.LockAnchor && !string.IsNullOrWhiteSpace(anchor.BlockId))
            {
                anchorBlockId = anchor.BlockId;
            }

            image.Layout.Anchor.BlockId = anchorBlockId;
            image.Layout.Anchor.InlineIndex = ResolveFloatingAnchorInlineIndex(document, anchorBlockId, selection, anchor);
            image.Layout.Anchor.Offset = ResolveFloatingAnchorOffset(anchorBlockId, selection, anchor);
            image.Layout.Anchor.Region = ResolveFloatingAnchorRegion(selection, anchor);
            anchor.BlockId = anchorBlockId;
            anchor.InlineIndex = image.Layout.Anchor.InlineIndex;
            anchor.Offset = image.Layout.Anchor.Offset;
            anchor.Scope = image.Layout.Anchor.Region;
            anchor.Layout = image.Layout;
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
            && string.Equals(anchor.ObjectBlockId, blockId, StringComparison.Ordinal));
    }

    private static void RemoveFloatingImageAnchor(DocumentEditorDocument document, string blockId)
    {
        var anchor = FindFloatingImageAnchor(document, blockId);
        if (anchor is not null)
        {
            document.Anchors.Remove(anchor);
        }
    }

    private static string? FindFallbackObjectAnchorBlockId(List<DocumentBlock> blocks, int removedIndex)
    {
        for (var index = removedIndex + 1; index < blocks.Count; index++)
        {
            if (IsObjectAnchorTargetBlock(blocks[index]))
            {
                return blocks[index].Id;
            }
        }

        for (var index = removedIndex - 1; index >= 0; index--)
        {
            if (IsObjectAnchorTargetBlock(blocks[index]))
            {
                return blocks[index].Id;
            }
        }

        return null;
    }

    private static bool IsObjectAnchorTargetBlock(DocumentBlock block)
        => block.Type is DocumentBlockType.Paragraph
            or DocumentBlockType.Heading
            or DocumentBlockType.List
            or DocumentBlockType.Quote;

    private static void RepairFloatingImageAnchorsAfterBlockRemoved(
        DocumentEditorDocument document,
        string removedBlockId,
        string? fallbackAnchorBlockId,
        WysiwygSelectionSnapshot? selection)
    {
        var anchors = document.Anchors
            .Where(anchor => anchor.Type == DocumentAnchorType.FloatingObject
                && string.Equals(anchor.BlockId, removedBlockId, StringComparison.Ordinal))
            .ToList();

        foreach (var anchor in anchors)
        {
            if (!string.IsNullOrWhiteSpace(fallbackAnchorBlockId))
            {
                anchor.BlockId = fallbackAnchorBlockId;
                anchor.Layout ??= new DocumentObjectLayout();
                anchor.Layout.Anchor.BlockId = fallbackAnchorBlockId;
                anchor.Layout.Anchor.InlineIndex = 0;
                anchor.Layout.Anchor.Offset = 0;
                anchor.InlineIndex = 0;
                anchor.Offset = 0;

                if (FindBlockById(document, anchor.ObjectBlockId, selection)?.Content is ImageBlockContent image)
                {
                    image.Layout.Anchor.BlockId = fallbackAnchorBlockId;
                    image.Layout.Anchor.InlineIndex = 0;
                    image.Layout.Anchor.Offset = 0;
                }

                continue;
            }

            document.Anchors.Remove(anchor);
            if (!string.IsNullOrWhiteSpace(anchor.ObjectBlockId)
                && TryFindBlockList(document, anchor.ObjectBlockId, out var objectBlocks, out var objectIndex, selection))
            {
                objectBlocks.RemoveAt(objectIndex);
            }
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
            && FindBlockById(document, selectedBlockId, selection) is not null)
        {
            return selectedBlockId;
        }

        if (!string.IsNullOrWhiteSpace(existingAnchor?.BlockId)
            && !string.Equals(existingAnchor.BlockId, imageBlockId, StringComparison.Ordinal)
            && FindBlockById(document, existingAnchor.BlockId, selection) is not null)
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

    private static int? ResolveFloatingAnchorInlineIndex(
        DocumentEditorDocument document,
        string anchorBlockId,
        WysiwygSelectionSnapshot? selection,
        DocumentAnchor? existingAnchor)
    {
        if (string.Equals(selection?.AnchorBlockId, anchorBlockId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(selection.AnchorInlineId)
            && FindBlockById(document, anchorBlockId, selection) is { } block
            && GetEditableInlines(block.Content) is { } inlines)
        {
            return FindInlineIndex(inlines, selection.AnchorInlineId);
        }

        return existingAnchor?.InlineIndex;
    }

    private static int? ResolveFloatingAnchorOffset(
        string anchorBlockId,
        WysiwygSelectionSnapshot? selection,
        DocumentAnchor? existingAnchor)
    {
        if (string.Equals(selection?.AnchorBlockId, anchorBlockId, StringComparison.Ordinal))
        {
            return selection.AnchorOffset;
        }

        return existingAnchor?.Offset;
    }

    private static DocumentRenditionAnchorScope ResolveFloatingAnchorRegion(
        WysiwygSelectionSnapshot? selection,
        DocumentAnchor? existingAnchor)
    {
        if (selection is null || string.IsNullOrWhiteSpace(selection.Region))
        {
            return existingAnchor?.Scope ?? DocumentRenditionAnchorScope.Body;
        }

        return selection.Region.Trim().ToLowerInvariant() switch
        {
            "header" => DocumentRenditionAnchorScope.Header,
            "footer" => DocumentRenditionAnchorScope.Footer,
            "footnote" => DocumentRenditionAnchorScope.Footnote,
            "endnote" => DocumentRenditionAnchorScope.Endnote,
            "body" => DocumentRenditionAnchorScope.Body,
            _ => existingAnchor?.Scope ?? DocumentRenditionAnchorScope.Body
        };
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

    private static void ApplyMarksToInlines(List<InlineContent> inlines, InlineMarkType markType, int startOffset, int endOffset, string? data, string? linkTitle)
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
            if (IsValueMark(markType) || markType == InlineMarkType.Link)
            {
                marked.Marks.RemoveAll(m => m.Type == markType);
            }

            if (!marked.Marks.Any(m => m.Type == markType))
            {
                if (markType != InlineMarkType.Link || DocumentLinkUtility.IsSafeHref(data))
                {
                    var mark = CreateMark(markType, data, linkTitle);
                    marked.Marks.Add(mark);
                }
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

    private static void RemoveStyleMarksFromInlines(List<InlineContent> inlines, int startOffset, int endOffset)
    {
        var styleMarks = new HashSet<InlineMarkType>
        {
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
            InlineMarkType.FontFamily,
            InlineMarkType.FontSize,
            InlineMarkType.TextColor,
            InlineMarkType.Highlight,
            InlineMarkType.Link
        };
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

            var cleared = SplitInline(inline, rangeStart, rangeEnd);
            cleared.Marks.RemoveAll(mark => styleMarks.Contains(mark.Type));
            newInlines.Add(cleared);

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

    private static InlineMark CreateMark(InlineMarkType markType, string? data, string? linkTitle)
    {
        var mark = new InlineMark { Type = markType };
        if (markType == InlineMarkType.Link && !string.IsNullOrWhiteSpace(data))
        {
            mark.Link = new LinkMarkData
            {
                Href = DocumentLinkUtility.NormalizeHref(data),
                Title = string.IsNullOrWhiteSpace(linkTitle) ? null : linkTitle.Trim()
            };
        }
        else if (IsValueMark(markType) && !string.IsNullOrWhiteSpace(data))
        {
            mark.Value = data;
        }

        return mark;
    }

    private static bool IsValueMark(InlineMarkType markType)
    {
        return markType is InlineMarkType.FontFamily
            or InlineMarkType.FontSize
            or InlineMarkType.TextColor
            or InlineMarkType.Highlight
            or InlineMarkType.CharacterSpacing
            or InlineMarkType.CharacterScale
            or InlineMarkType.Kerning;
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
            DocumentFieldRun field => new DocumentFieldRun
            {
                Id = field.Id,
                FieldType = field.FieldType,
                Format = field.Format,
                FallbackText = field.FallbackText,
                DisplayText = field.DisplayText,
                Marks = field.Marks.ToList()
            },
            DocumentNoteReferenceRun note => new DocumentNoteReferenceRun
            {
                Id = note.Id,
                NoteId = note.NoteId,
                NoteType = note.NoteType,
                DisplayMarker = note.DisplayMarker,
                Marks = note.Marks.ToList()
            },
            DocumentDrawingRun drawing => new DocumentDrawingRun
            {
                Id = drawing.Id,
                ObjectId = drawing.ObjectId,
                Kind = drawing.Kind,
                Source = drawing.Source,
                Url = drawing.Url,
                AssetId = drawing.AssetId,
                AltText = drawing.AltText,
                IsDecorative = drawing.IsDecorative,
                Caption = drawing.Caption,
                Size = CloneDocumentEditorValue(drawing.Size),
                NaturalSize = CloneDocumentEditorValue(drawing.NaturalSize),
                Layout = CloneDocumentEditorValue(drawing.Layout),
                LinkUrl = drawing.LinkUrl,
                Metadata = drawing.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value),
                Marks = drawing.Marks.Select(CloneMark).ToList()
            },
            _ => new TextRun { Text = GetInlineText(inline) }
        };
    }

    private static T CloneDocumentEditorValue<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private static List<InlineMark> CloneTypingMarks(IEnumerable<InlineMark> marks)
    {
        return marks
            .Where(mark => mark.Type != InlineMarkType.Revision)
            .Select(CloneMark)
            .ToList();
    }

    private static InlineMark CloneMark(InlineMark mark)
    {
        return new InlineMark
        {
            Type = mark.Type,
            Link = mark.Link is not null ? new LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title } : null,
            CommentAnchor = mark.CommentAnchor is not null
                ? new CommentAnchorMarkData { CommentId = mark.CommentAnchor.CommentId, AnchorId = mark.CommentAnchor.AnchorId }
                : null,
            RevisionId = mark.RevisionId,
            Value = mark.Value
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
            case DocumentFieldRun fieldRun:
                fieldRun.DisplayText = slice;
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

        var orderedA = a.OrderBy(m => m.Type).ThenBy(m => m.Value).ToList();
        var orderedB = b.OrderBy(m => m.Type).ThenBy(m => m.Value).ToList();

        return orderedA.Zip(orderedB).All(pair =>
            pair.First.Type == pair.Second.Type
            && pair.First.Link?.Href == pair.Second.Link?.Href
            && pair.First.Link?.Title == pair.Second.Link?.Title
            && pair.First.CommentAnchor?.CommentId == pair.Second.CommentAnchor?.CommentId
            && pair.First.CommentAnchor?.AnchorId == pair.Second.CommentAnchor?.AnchorId
            && pair.First.RevisionId == pair.Second.RevisionId
            && pair.First.Value == pair.Second.Value);
    }

    private static WysiwygPatch UpgradePatch(WysiwygPatch patch)
    {
        // Graceful upgrade: return a copy with the current protocol version.
        // Concrete upgrade logic will be added when future protocol versions are introduced.
        return new WysiwygPatch
        {
            Type = patch.Type,
            OperationId = patch.OperationId,
            Data = patch.Data,
            LinkTitle = patch.LinkTitle,
            Inline = patch.Inline,
            Selection = patch.Selection,
            BeforeSelection = patch.BeforeSelection,
            AfterSelection = patch.AfterSelection,
            TransactionId = patch.TransactionId,
            ProtocolVersion = SupportedProtocolVersion,
            Html = patch.Html,
            Plain = patch.Plain,
            HasImage = patch.HasImage,
            MarkType = patch.MarkType,
            BlockType = patch.BlockType,
            Block = patch.Block,
            ParagraphProperties = patch.ParagraphProperties,
            DeleteLength = patch.DeleteLength,
            HeadingLevel = patch.HeadingLevel,
            RevisionId = patch.RevisionId,
            RevisionType = patch.RevisionType
        };
    }
}
