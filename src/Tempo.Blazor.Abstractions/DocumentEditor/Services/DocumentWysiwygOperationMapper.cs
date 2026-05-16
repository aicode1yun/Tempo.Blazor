using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Maps WYSIWYG patches to granular document operations.</summary>
public sealed class DocumentWysiwygOperationMapper
{
    /// <summary>Creates an operation batch from a WYSIWYG patch.</summary>
    public DocumentOperationBatch CreateBatch(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(patch);

        var operation = CreateOperation(document, patch, metadata);
        return new DocumentOperationBatch
        {
            DocumentId = document.DocumentId,
            Operations = operation is null ? [] : [operation]
        };
    }

    /// <summary>Creates a single operation from a WYSIWYG patch when the patch has a granular representation.</summary>
    public DocumentOperation? CreateOperation(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(patch);

        var operation = patch.Type switch
        {
            "InsertText" => CreateInsertText(document, patch, metadata),
            "DeleteRange" => CreateDeleteText(document, patch, DeleteStart.Range, metadata),
            "DeleteContentBackward" => CreateDeleteText(document, patch, DeleteStart.Backward, metadata),
            "DeleteContentForward" => CreateDeleteText(document, patch, DeleteStart.Forward, metadata),
            "ToggleMark" => CreateToggleMark(document, patch, metadata),
            "SetMarks" => CreateSetMark(document, patch, metadata),
            "ClearFormatting" => CreateClearFormatting(document, patch, metadata),
            "SetParagraphProperties" => CreateSetParagraphProperties(patch, metadata),
            "InsertBlock" => CreateInsertBlock(document, patch, metadata),
            "UpdateBlock" => CreateUpdateBlock(document, patch, metadata),
            "MoveBlock" => CreateMoveBlock(patch, metadata),
            "RemoveBlock" => CreateRemoveBlock(patch, metadata),
            _ => null
        };
        if (operation is not null && !string.IsNullOrWhiteSpace(patch.OperationId))
        {
            operation.OperationId = patch.OperationId;
        }

        return operation;
    }

    private static DocumentOperation? CreateInsertText(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        if (string.IsNullOrEmpty(patch.Data) || string.IsNullOrWhiteSpace(patch.Selection?.AnchorBlockId))
        {
            return null;
        }

        var target = CreateTarget(document, patch.Selection, patch.Selection.AnchorOffset, patch.Data.Length);
        if (IsRevisionPatch(patch, "Insertion"))
        {
            return CreateRevisionOperation(DocumentRevisionType.Insertion, target, patch, metadata, patch.Data);
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertText,
            Target = target,
            Text = patch.Data,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateDeleteText(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DeleteStart startMode,
        DocumentOperationMetadata? metadata)
    {
        if (string.IsNullOrWhiteSpace(patch.Selection?.AnchorBlockId))
        {
            return null;
        }

        var length = GetDeleteLength(patch);
        if (length <= 0)
        {
            return null;
        }

        var offset = patch.Selection.AnchorOffset;
        if (startMode == DeleteStart.Backward)
        {
            offset -= length;
        }

        offset = Math.Max(0, offset);
        var text = patch.Data;
        if (string.IsNullOrEmpty(text))
        {
            text = ReadText(document, patch.Selection, offset, length);
        }

        var target = CreateTarget(document, patch.Selection, offset, length);
        if (IsRevisionPatch(patch, "Deletion"))
        {
            return CreateRevisionOperation(DocumentRevisionType.Deletion, target, patch, metadata, text);
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.DeleteText,
            Target = target,
            Text = text,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateToggleMark(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        var target = CreateMarkTarget(document, patch);
        if (target is null)
        {
            return null;
        }

        var mark = CreateMark(patch);
        if (mark is null)
        {
            return null;
        }

        var hasMark = RangeHasMark(document, patch.Selection!, mark);
        return new DocumentOperation
        {
            Type = hasMark ? DocumentOperationType.RemoveInlineMark : DocumentOperationType.AddInlineMark,
            Target = target,
            Mark = mark,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateSetMark(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        var target = CreateMarkTarget(document, patch);
        if (target is null)
        {
            return null;
        }

        var mark = CreateMark(patch);
        if (mark is null)
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.AddInlineMark,
            Target = target,
            Mark = mark,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateClearFormatting(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        var target = CreateMarkTarget(document, patch);
        if (target is null)
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = target,
            AttributeName = "clearFormatting",
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateInsertBlock(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        if (patch.Block is null)
        {
            return null;
        }

        var block = Clone(patch.Block);
        var order = block.Order == 0
            ? CalculateInsertedBlockOrder(document, patch.Selection)
            : block.Order;
        block.Order = order;

        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertBlock,
            Target = new DocumentOperationTarget
            {
                BlockId = block.Id,
                SectionId = block.SectionId,
                Order = order
            },
            Block = block,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateUpdateBlock(
        DocumentEditorDocument document,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        if (patch.Block is null)
        {
            return null;
        }

        var updated = Clone(patch.Block);
        var existing = document.Blocks.FirstOrDefault(block => block.Id == updated.Id);
        if (existing is null)
        {
            return new DocumentOperation
            {
                Type = DocumentOperationType.UpdateBlock,
                Target = new DocumentOperationTarget
                {
                    BlockId = updated.Id,
                    SectionId = updated.SectionId,
                    Order = updated.Order == 0 ? null : updated.Order
                },
                Block = updated,
                Metadata = CreateMetadata(metadata, patch)
            };
        }

        if (TryCreateHeadingLevelOperation(existing, updated, patch, metadata, out var headingOperation))
        {
            return headingOperation;
        }

        if (TryCreateTableCellTextOperation(existing, updated, patch, metadata, out var tableCellOperation))
        {
            return tableCellOperation;
        }

        if (BlocksEqual(existing, updated))
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.UpdateBlock,
            Target = new DocumentOperationTarget
            {
                BlockId = updated.Id,
                SectionId = updated.SectionId,
                Order = updated.Order == 0 ? null : updated.Order
            },
            Block = updated,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateRemoveBlock(
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        var blockId = string.IsNullOrWhiteSpace(patch.Selection?.AnchorBlockId)
            ? patch.Block?.Id
            : patch.Selection.AnchorBlockId;
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.DeleteBlock,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                TableCellId = patch.Selection?.ActiveTableCellId
            },
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateMoveBlock(
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        var blockId = patch.Block?.Id ?? patch.Selection?.AnchorBlockId;
        var order = patch.Block?.Order;
        if (string.IsNullOrWhiteSpace(blockId) || order is null or 0)
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.MoveBlock,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                SectionId = patch.Block?.SectionId,
                Order = order
            },
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static DocumentOperation? CreateSetParagraphProperties(
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata)
    {
        if (patch.ParagraphProperties is null || string.IsNullOrWhiteSpace(patch.Selection?.AnchorBlockId))
        {
            return null;
        }

        return new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget
            {
                BlockId = patch.Selection.AnchorBlockId,
                TableCellId = patch.Selection.ActiveTableCellId
            },
            AttributeName = "paragraphProperties",
            AttributeValueJson = JsonSerializer.Serialize(patch.ParagraphProperties, DocumentEditorJson.Options),
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    /// <summary>Creates an operation that accepts a pending revision.</summary>
    public DocumentOperation CreateAcceptRevision(DocumentRevision revision, DocumentOperationMetadata? metadata = null)
        => CreateReviewRevisionOperation(DocumentOperationType.AcceptRevision, revision, metadata);

    /// <summary>Creates an operation that rejects a pending revision.</summary>
    public DocumentOperation CreateRejectRevision(DocumentRevision revision, DocumentOperationMetadata? metadata = null)
        => CreateReviewRevisionOperation(DocumentOperationType.RejectRevision, revision, metadata);

    private static DocumentOperation CreateReviewRevisionOperation(
        DocumentOperationType type,
        DocumentRevision revision,
        DocumentOperationMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var operationMetadata = metadata is null ? new DocumentOperationMetadata() : Clone(metadata);
        operationMetadata.RevisionId = revision.Id;
        operationMetadata.RevisionType = revision.Type.ToString();
        if (operationMetadata.LogicalTimestamp <= 0)
        {
            operationMetadata.LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return new DocumentOperation
        {
            Type = type,
            Target = new DocumentOperationTarget
            {
                BlockId = revision.Range.BlockId,
                InlineIndex = revision.Range.StartInlineIndex,
                Offset = revision.Range.StartOffset,
                Length = revision.Range.EndOffset is not null && revision.Range.StartOffset is not null
                    ? Math.Max(0, revision.Range.EndOffset.Value - revision.Range.StartOffset.Value)
                    : null
            },
            Revision = Clone(revision),
            Metadata = operationMetadata
        };
    }

    private static DocumentOperation CreateRevisionOperation(
        DocumentRevisionType type,
        DocumentOperationTarget target,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata,
        string? text)
    {
        var revisionId = string.IsNullOrWhiteSpace(patch.RevisionId)
            ? Guid.NewGuid().ToString("N")
            : patch.RevisionId;
        var payload = text ?? string.Empty;
        var range = new DocumentRevisionRange
        {
            BlockId = target.BlockId,
            StartInlineIndex = target.InlineIndex,
            EndInlineIndex = target.InlineIndex,
            StartOffset = target.Offset,
            EndOffset = (target.Offset ?? 0) + (target.Length ?? payload.Length)
        };

        var revision = new DocumentRevision
        {
            Id = revisionId,
            Type = type,
            Range = range,
            Author = new DocumentRevisionAuthor
            {
                Id = metadata?.AuthorId ?? string.Empty,
                DisplayName = metadata?.AuthorId ?? string.Empty
            },
            CreatedAt = DateTimeOffset.UtcNow,
            Action = DocumentRevisionAction.Pending,
            PayloadJson = payload
        };

        return new DocumentOperation
        {
            Type = DocumentOperationType.CreateRevision,
            Target = target,
            Text = payload,
            Revision = revision,
            Metadata = CreateMetadata(metadata, patch)
        };
    }

    private static bool IsRevisionPatch(WysiwygPatch patch, string revisionType)
    {
        return string.Equals(patch.RevisionType, revisionType, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(patch.RevisionId)
                && string.Equals(patch.RevisionType, revisionType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryCreateHeadingLevelOperation(
        DocumentBlock existing,
        DocumentBlock updated,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata,
        out DocumentOperation? operation)
    {
        operation = null;
        if (existing.Content is not HeadingBlockContent existingHeading
            || updated.Content is not HeadingBlockContent updatedHeading
            || existingHeading.Level == updatedHeading.Level)
        {
            return false;
        }

        var normalized = Clone(updated);
        if (normalized.Content is HeadingBlockContent normalizedHeading)
        {
            normalizedHeading.Level = existingHeading.Level;
        }

        if (!BlocksEqual(existing, normalized))
        {
            return false;
        }

        operation = new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = updated.Id },
            AttributeName = "headingLevel",
            AttributeValueJson = JsonSerializer.Serialize(updatedHeading.Level, DocumentEditorJson.Options),
            Metadata = CreateMetadata(metadata, patch)
        };
        return true;
    }

    private static bool TryCreateTableCellTextOperation(
        DocumentBlock existing,
        DocumentBlock updated,
        WysiwygPatch patch,
        DocumentOperationMetadata? metadata,
        out DocumentOperation? operation)
    {
        operation = null;
        if (existing.Content is not TableBlockContent existingTable
            || updated.Content is not TableBlockContent updatedTable)
        {
            return false;
        }

        var changedCells = FindChangedTableCells(existingTable, updatedTable).ToList();
        if (changedCells.Count != 1)
        {
            return false;
        }

        var changed = changedCells[0];
        if (!AreSameCellShape(changed.Existing, changed.Updated))
        {
            return false;
        }

        var existingText = ReadCellPlainText(changed.Existing);
        var updatedText = ReadCellPlainText(changed.Updated);
        if (string.Equals(existingText, updatedText, StringComparison.Ordinal)
            || !CellDiffIsOnlyPlainText(changed.Existing, changed.Updated, updatedText))
        {
            return false;
        }

        operation = new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget
            {
                BlockId = updated.Id,
                TableCellId = changed.Updated.Id
            },
            AttributeName = "table.cell.text",
            AttributeValueJson = JsonSerializer.Serialize(updatedText, DocumentEditorJson.Options),
            Metadata = CreateMetadata(metadata, patch)
        };
        return true;
    }

    private static DocumentOperationTarget? CreateMarkTarget(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var selection = patch.Selection;
        if (selection is null || string.IsNullOrWhiteSpace(selection.AnchorBlockId))
        {
            return null;
        }

        if (!selection.IsCollapsed
            && (!string.Equals(selection.AnchorBlockId, selection.FocusBlockId, StringComparison.Ordinal)
                || !string.Equals(selection.AnchorInlineId, selection.FocusInlineId, StringComparison.Ordinal)))
        {
            return null;
        }

        var start = Math.Min(selection.AnchorOffset, selection.IsCollapsed ? selection.AnchorOffset : selection.FocusOffset);
        var end = selection.IsCollapsed
            ? selection.AnchorOffset
            : Math.Max(selection.AnchorOffset, selection.FocusOffset);
        var length = end - start;
        if (length <= 0)
        {
            return null;
        }

        return CreateTarget(document, selection, start, length);
    }

    private static InlineMark? CreateMark(WysiwygPatch patch)
    {
        var markType = ParseMarkType(patch.MarkType);
        var mark = new InlineMark
        {
            Type = markType,
            RevisionId = patch.RevisionId
        };

        if (markType == InlineMarkType.Link)
        {
            var href = DocumentLinkUtility.NormalizeHref(patch.Data);
            if (!DocumentLinkUtility.IsSafeHref(href))
            {
                return null;
            }

            mark.Link = new LinkMarkData
            {
                Href = href,
                Title = string.IsNullOrWhiteSpace(patch.LinkTitle) ? null : patch.LinkTitle.Trim()
            };
        }
        else if (markType == InlineMarkType.CommentAnchor && !string.IsNullOrWhiteSpace(patch.Data))
        {
            mark.CommentAnchor = new CommentAnchorMarkData
            {
                CommentId = patch.Data,
                AnchorId = patch.Data
            };
        }
        else if (markType == InlineMarkType.Revision)
        {
            mark.Value = string.IsNullOrWhiteSpace(patch.RevisionType) ? patch.Data : patch.RevisionType;
        }
        else if (!string.IsNullOrWhiteSpace(patch.Data))
        {
            mark.Value = patch.Data;
        }

        return mark;
    }

    private static DocumentOperationTarget CreateTarget(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot selection,
        int offset,
        int length)
    {
        var inlineIndex = ResolveInlineIndex(document, selection.AnchorBlockId, selection.AnchorInlineId);
        return new DocumentOperationTarget
        {
            BlockId = selection.AnchorBlockId,
            TableCellId = selection.ActiveTableCellId,
            InlineId = selection.AnchorInlineId,
            InlineIndex = inlineIndex,
            Offset = Math.Max(0, offset),
            Length = Math.Max(0, length)
        };
    }

    private static double CalculateInsertedBlockOrder(DocumentEditorDocument document, WysiwygSelectionSnapshot? selection)
    {
        if (document.Blocks.Count == 0)
        {
            return 1;
        }

        var anchorIndex = string.IsNullOrWhiteSpace(selection?.AnchorBlockId)
            ? -1
            : document.Blocks.FindIndex(block => string.Equals(block.Id, selection.AnchorBlockId, StringComparison.Ordinal));
        if (anchorIndex < 0)
        {
            return document.Blocks.Max(block => block.Order) + 1;
        }

        var anchor = document.Blocks[anchorIndex];
        var next = anchorIndex + 1 < document.Blocks.Count ? document.Blocks[anchorIndex + 1] : null;
        if (next is null)
        {
            return anchor.Order + 1;
        }

        return (anchor.Order + next.Order) / 2d;
    }

    private static int ResolveInlineIndex(DocumentEditorDocument document, string? blockId, string? inlineId)
    {
        var inlines = GetEditableInlines(document.Blocks.FirstOrDefault(block => block.Id == blockId)?.Content);
        if (inlines is null || string.IsNullOrWhiteSpace(inlineId))
        {
            return 0;
        }

        var index = inlines.FindIndex(inline => string.Equals(inline.Id, inlineId, StringComparison.Ordinal));
        return index < 0 ? 0 : index;
    }

    private static string? ReadText(DocumentEditorDocument document, WysiwygSelectionSnapshot selection, int offset, int length)
    {
        var inlines = GetEditableInlines(document.Blocks.FirstOrDefault(block => block.Id == selection.AnchorBlockId)?.Content);
        if (inlines is null)
        {
            return null;
        }

        var inlineIndex = ResolveInlineIndex(document, selection.AnchorBlockId, selection.AnchorInlineId);
        if (inlineIndex < 0 || inlineIndex >= inlines.Count || inlines[inlineIndex] is not TextRun run)
        {
            return null;
        }

        var start = Math.Clamp(offset, 0, run.Text.Length);
        var end = Math.Clamp(start + length, start, run.Text.Length);
        return run.Text[start..end];
    }

    private static int GetDeleteLength(WysiwygPatch patch)
    {
        if (patch.DeleteLength > 0)
        {
            return patch.DeleteLength;
        }

        return patch.Data?.Length ?? 0;
    }

    private static bool RangeHasMark(DocumentEditorDocument document, WysiwygSelectionSnapshot selection, InlineMark mark)
    {
        var inlines = GetEditableInlines(document.Blocks.FirstOrDefault(block => block.Id == selection.AnchorBlockId)?.Content);
        if (inlines is null)
        {
            return false;
        }

        var inlineIndex = ResolveInlineIndex(document, selection.AnchorBlockId, selection.AnchorInlineId);
        if (inlineIndex < 0 || inlineIndex >= inlines.Count)
        {
            return false;
        }

        var startOffset = Math.Min(selection.AnchorOffset, selection.IsCollapsed ? selection.AnchorOffset : selection.FocusOffset);
        var endOffset = selection.IsCollapsed
            ? selection.AnchorOffset
            : Math.Max(selection.AnchorOffset, selection.FocusOffset);

        var inline = inlines[inlineIndex];
        var textLength = GetInlineText(inline).Length;
        return endOffset > startOffset
            && startOffset < textLength
            && inline.Marks.Any(existing => SameMark(existing, mark));
    }

    private static string GetInlineText(InlineContent inline)
    {
        return inline switch
        {
            TextRun run => run.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        };
    }

    private static bool SameMark(InlineMark left, InlineMark right)
    {
        return left.Type == right.Type
            && left.Link?.Href == right.Link?.Href
            && left.Link?.Title == right.Link?.Title
            && left.CommentAnchor?.CommentId == right.CommentAnchor?.CommentId
            && left.CommentAnchor?.AnchorId == right.CommentAnchor?.AnchorId
            && left.RevisionId == right.RevisionId
            && left.Value == right.Value;
    }

    private static bool BlocksEqual(DocumentBlock left, DocumentBlock right)
    {
        var leftJson = JsonSerializer.Serialize(left, DocumentEditorJson.Options);
        var rightJson = JsonSerializer.Serialize(right, DocumentEditorJson.Options);
        return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
    }

    private static IEnumerable<(TableCellContent Existing, TableCellContent Updated)> FindChangedTableCells(
        TableBlockContent existing,
        TableBlockContent updated)
    {
        var existingById = existing.Rows.SelectMany(row => row.Cells).ToDictionary(cell => cell.Id, StringComparer.Ordinal);
        foreach (var updatedCell in updated.Rows.SelectMany(row => row.Cells))
        {
            if (!existingById.TryGetValue(updatedCell.Id, out var existingCell)
                || !CellEquals(existingCell, updatedCell))
            {
                yield return (existingCell ?? new TableCellContent { Id = updatedCell.Id }, updatedCell);
            }
        }

        var updatedIds = updated.Rows.SelectMany(row => row.Cells).Select(cell => cell.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var removed in existing.Rows.SelectMany(row => row.Cells).Where(cell => !updatedIds.Contains(cell.Id)))
        {
            yield return (removed, new TableCellContent { Id = removed.Id });
        }
    }

    private static bool CellEquals(TableCellContent left, TableCellContent right)
    {
        var leftJson = JsonSerializer.Serialize(left, DocumentEditorJson.Options);
        var rightJson = JsonSerializer.Serialize(right, DocumentEditorJson.Options);
        return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
    }

    private static bool AreSameCellShape(TableCellContent left, TableCellContent right)
    {
        return left.ColumnSpan == right.ColumnSpan
            && left.RowSpan == right.RowSpan
            && left.Merge.IsOrigin == right.Merge.IsOrigin
            && string.Equals(left.Merge.OriginCellId, right.Merge.OriginCellId, StringComparison.Ordinal)
            && left.Blocks.Count == right.Blocks.Count;
    }

    private static bool CellDiffIsOnlyPlainText(TableCellContent existing, TableCellContent updated, string updatedText)
    {
        if (existing.Blocks.Count != updated.Blocks.Count)
        {
            return false;
        }

        var normalized = Clone(updated);
        SetCellPlainText(normalized, ReadCellPlainText(existing));
        return CellEquals(existing, normalized)
            || (existing.Blocks.Count == 0 && updated.Blocks.Count == 0 && string.IsNullOrEmpty(updatedText));
    }

    private static string ReadCellPlainText(TableCellContent cell)
    {
        var paragraph = cell.Blocks.FirstOrDefault(block => block.Content is ParagraphBlockContent)
            ?? cell.Blocks.FirstOrDefault();
        return paragraph?.Content is null
            ? string.Empty
            : string.Concat(GetEditableInlines(paragraph.Content)?.Select(GetInlineText) ?? []);
    }

    private static void SetCellPlainText(TableCellContent cell, string text)
    {
        var paragraph = cell.Blocks.FirstOrDefault(block => block.Content is ParagraphBlockContent)
            ?? cell.Blocks.FirstOrDefault();
        if (paragraph is null)
        {
            return;
        }

        paragraph.Type = DocumentBlockType.Paragraph;
        if (paragraph.Content is not ParagraphBlockContent content)
        {
            paragraph.Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = text }]
            };
            return;
        }

        if (content.Inlines.Count == 1 && content.Inlines[0] is TextRun run)
        {
            run.Text = text;
            return;
        }

        content.Inlines.Clear();
        content.Inlines.Add(new TextRun { Text = text });
    }

    private static InlineMarkType ParseMarkType(string? markType)
    {
        return Enum.TryParse<InlineMarkType>(markType, ignoreCase: true, out var result)
            ? result
            : InlineMarkType.Bold;
    }

    private static DocumentOperationMetadata CreateMetadata(DocumentOperationMetadata? metadata, WysiwygPatch patch)
    {
        var result = metadata is null ? new DocumentOperationMetadata() : Clone(metadata);
        result.TransactionId = patch.TransactionId;
        result.RevisionId = patch.RevisionId;
        result.RevisionType = patch.RevisionType;
        if (result.LogicalTimestamp <= 0)
        {
            result.LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return result;
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

    private static DocumentOperationMetadata Clone(DocumentOperationMetadata metadata)
    {
        return new DocumentOperationMetadata
        {
            AuthorId = metadata.AuthorId,
            OriginSessionId = metadata.OriginSessionId,
            LogicalTimestamp = metadata.LogicalTimestamp,
            ClientId = metadata.ClientId,
            TransactionId = metadata.TransactionId,
            RevisionId = metadata.RevisionId,
            RevisionType = metadata.RevisionType,
            CreatedAt = metadata.CreatedAt
        };
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private enum DeleteStart
    {
        Range,
        Backward,
        Forward
    }
}
