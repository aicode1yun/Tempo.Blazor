using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Applies low-level document operations to a document snapshot.</summary>
public class DocumentOperationApplier
{
    /// <summary>Applies a batch to a document.</summary>
    public DocumentOperationValidationResult Apply(DocumentEditorDocument document, DocumentOperationBatch batch)
    {
        var validation = DocumentOperationLog.Validate(batch);
        if (!validation.IsValid)
        {
            return validation;
        }

        if (!string.Equals(document.DocumentId, batch.DocumentId, StringComparison.Ordinal))
        {
            return DocumentOperationValidationResult.Invalid("Batch document id does not match target document.");
        }

        foreach (var operation in batch.Operations)
        {
            var result = Apply(document, operation);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Applies a single operation to a document.</summary>
    public DocumentOperationValidationResult Apply(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.SchemaVersion != DocumentEditorDocument.CurrentSchemaVersion)
        {
            return DocumentOperationValidationResult.Invalid($"Unsupported operation schema version {operation.SchemaVersion}.");
        }

        return operation.Type switch
        {
            DocumentOperationType.InsertText => ApplyInsertText(document, operation),
            DocumentOperationType.DeleteText => ApplyDeleteText(document, operation),
            DocumentOperationType.AddInlineMark => ApplyMark(document, operation, add: true),
            DocumentOperationType.RemoveInlineMark => ApplyMark(document, operation, add: false),
            DocumentOperationType.InsertBlock => ApplyInsertBlock(document, operation),
            DocumentOperationType.DeleteBlock => ApplyDeleteBlock(document, operation),
            DocumentOperationType.MoveBlock => ApplyMoveBlock(document, operation),
            DocumentOperationType.SetBlockAttribute => ApplySetAttribute(document, operation),
            DocumentOperationType.UpdateBlock => ApplyUpdateBlock(document, operation),
            DocumentOperationType.MoveDrawingObject => ApplyMoveDrawingObject(document, operation),
            DocumentOperationType.CreateRevision => ApplyCreateRevision(document, operation),
            DocumentOperationType.AcceptRevision => ApplyReviewRevision(document, operation, DocumentRevisionAction.Accepted),
            DocumentOperationType.RejectRevision => ApplyReviewRevision(document, operation, DocumentRevisionAction.Rejected),
            _ => DocumentOperationValidationResult.Invalid("Unsupported operation type.")
        };
    }

    private static DocumentOperationValidationResult ApplyInsertText(DocumentEditorDocument document, DocumentOperation operation)
    {
        var block = FindBlock(document, operation.Target.BlockId, operation.Target.TableCellId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("InsertText target block was not found or is not text-based.");
        }

        var run = EnsureTextRun(inlines, ResolveInlineIndex(inlines, operation.Target));
        var offset = Math.Clamp(operation.Target.Offset ?? run.Text.Length, 0, run.Text.Length);
        run.Text = run.Text.Insert(offset, operation.Text ?? string.Empty);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyDeleteText(DocumentEditorDocument document, DocumentOperation operation)
    {
        var block = FindBlock(document, operation.Target.BlockId, operation.Target.TableCellId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("DeleteText target block was not found or is not text-based.");
        }

        var inlineIndex = ResolveInlineIndex(inlines, operation.Target);
        if (inlineIndex < 0 || inlineIndex >= inlines.Count || inlines[inlineIndex] is not TextRun run)
        {
            return DocumentOperationValidationResult.Invalid("DeleteText target inline was not found.");
        }

        var offset = Math.Clamp(operation.Target.Offset ?? 0, 0, run.Text.Length);
        var length = operation.Target.Length ?? (operation.Text ?? string.Empty).Length;
        length = Math.Min(length, run.Text.Length - offset);
        run.Text = run.Text.Remove(offset, length);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyMark(DocumentEditorDocument document, DocumentOperation operation, bool add)
    {
        var block = FindBlock(document, operation.Target.BlockId, operation.Target.TableCellId);
        var inlines = GetInlineList(block?.Content);
        var inlineIndex = inlines is null ? 0 : ResolveInlineIndex(inlines, operation.Target);
        if (block is null || inlines is null || operation.Mark is null || inlineIndex < 0 || inlineIndex >= inlines.Count)
        {
            return DocumentOperationValidationResult.Invalid("Mark target was not found.");
        }

        if (operation.Target.Offset is not null && operation.Target.Length is > 0)
        {
            ApplyMarkRange(inlines, operation, add, inlineIndex);
            return DocumentOperationValidationResult.Valid();
        }

        ApplyMarkToInline(inlines[inlineIndex], operation.Mark, add);

        return DocumentOperationValidationResult.Valid();
    }

    private static void ApplyMarkRange(List<InlineContent> inlines, DocumentOperation operation, bool add, int inlineIndex)
    {
        var length = operation.Target.Length ?? 0;
        if (operation.Mark is null || length <= 0)
        {
            return;
        }

        var start = ResolveAbsoluteRangeStart(inlines, operation.Target, inlineIndex);
        ApplyMarkAbsoluteRange(inlines, start, start + length, operation.Mark, add);
    }

    private static int ResolveAbsoluteRangeStart(List<InlineContent> inlines, DocumentOperationTarget target, int inlineIndex)
    {
        var offset = Math.Max(0, target.Offset ?? 0);
        if (string.IsNullOrWhiteSpace(target.InlineId))
        {
            return offset;
        }

        var clampedIndex = Math.Clamp(inlineIndex, 0, Math.Max(0, inlines.Count - 1));
        return inlines.Take(clampedIndex).Sum(inline => GetInlineText(inline).Length) + offset;
    }

    private static void ApplyMarkAbsoluteRange(List<InlineContent> inlines, int start, int end, InlineMark mark, bool add)
    {
        var rangeStart = Math.Max(0, Math.Min(start, end));
        var rangeEnd = Math.Max(rangeStart, Math.Max(start, end));
        var currentOffset = 0;
        var newInlines = new List<InlineContent>();

        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = currentOffset;
            var inlineEnd = inlineStart + text.Length;
            currentOffset = inlineEnd;

            if (inlineEnd <= rangeStart || inlineStart >= rangeEnd || text.Length == 0)
            {
                newInlines.Add(Clone(inline));
                continue;
            }

            var localStart = Math.Max(0, rangeStart - inlineStart);
            var localEnd = Math.Min(text.Length, rangeEnd - inlineStart);
            localStart = ClampTextBoundary(text, localStart, isEnd: false);
            localEnd = ClampTextBoundary(text, localEnd, isEnd: true);
            if (localEnd < localStart)
            {
                localEnd = localStart;
            }

            if (localStart > 0)
            {
                newInlines.Add(SplitInline(inline, 0, localStart));
            }

            var marked = SplitInline(inline, localStart, localEnd);
            ApplyMarkToInline(marked, mark, add);
            if (GetInlineText(marked).Length > 0)
            {
                newInlines.Add(marked);
            }

            if (localEnd < text.Length)
            {
                newInlines.Add(SplitInline(inline, localEnd, text.Length));
            }
        }

        inlines.Clear();
        inlines.AddRange(MergeAdjacentTextRuns(newInlines));
    }

    private static void ApplyMarkToInline(InlineContent inline, InlineMark mark, bool add)
    {
        if (add)
        {
            if (IsSingletonFormattingMark(mark.Type))
            {
                inline.Marks.RemoveAll(existing => existing.Type == mark.Type);
            }

            if (!inline.Marks.Any(existing => SameMark(existing, mark)))
            {
                inline.Marks.Add(Clone(mark));
            }
        }
        else
        {
            inline.Marks.RemoveAll(existing => ShouldRemoveMark(existing, mark));
        }

        inline.Marks = NormalizeMarks(inline.Marks);
    }

    private static bool ShouldRemoveMark(InlineMark existing, InlineMark requested)
    {
        return IsFormattingMark(requested)
            ? existing.Type == requested.Type
            : SameMark(existing, requested);
    }

    private static bool IsSingletonFormattingMark(InlineMarkType type)
    {
        return type is InlineMarkType.Link
            or InlineMarkType.Highlight
            or InlineMarkType.TextColor
            or InlineMarkType.FontFamily
            or InlineMarkType.FontSize;
    }

    private static (int InlineIndex, int RangeStart, int RangeEnd)? ResolveMarkRange(
        List<InlineContent> inlines,
        DocumentOperation operation,
        int inlineIndex)
    {
        if (inlineIndex < 0 || inlineIndex >= inlines.Count)
        {
            return null;
        }

        var startOffset = operation.Target.Offset ?? 0;
        var length = operation.Target.Length ?? 0;
        var inline = inlines[inlineIndex];
        var text = GetInlineText(inline);
        var targetHasStableInlineId = !string.IsNullOrWhiteSpace(operation.Target.InlineId);
        if (targetHasStableInlineId)
        {
            var matchingInlineIndexes = inlines
                .Select((item, index) => new { Inline = item, Index = index })
                .Where(item => string.Equals(item.Inline.Id, operation.Target.InlineId, StringComparison.Ordinal))
                .Select(item => item.Index)
                .ToList();
            if (matchingInlineIndexes.Count > 1)
            {
                var matchingAbsoluteStart = Math.Max(0, startOffset);
                var matchingCumulative = 0;
                foreach (var candidateIndex in matchingInlineIndexes)
                {
                    var candidateText = GetInlineText(inlines[candidateIndex]);
                    var candidateLength = candidateText.Length;
                    if (matchingAbsoluteStart < matchingCumulative + candidateLength || candidateIndex == matchingInlineIndexes[^1])
                    {
                        var rangeStart = Math.Clamp(matchingAbsoluteStart - matchingCumulative, 0, candidateLength);
                        var rangeEnd = Math.Clamp(rangeStart + length, rangeStart, candidateLength);
                        return (candidateIndex, rangeStart, rangeEnd);
                    }

                    matchingCumulative += candidateLength;
                }
            }
        }

        if (targetHasStableInlineId || inlineIndex != 0 || startOffset < text.Length || inlines.Count <= 1)
        {
            var rangeStart = Math.Clamp(startOffset, 0, text.Length);
            var rangeEnd = Math.Clamp(startOffset + length, rangeStart, text.Length);
            return (inlineIndex, rangeStart, rangeEnd);
        }

        var absoluteStart = Math.Max(0, startOffset);
        var cumulative = 0;
        for (var i = 0; i < inlines.Count; i++)
        {
            var candidateText = GetInlineText(inlines[i]);
            var candidateLength = candidateText.Length;
            if (absoluteStart < cumulative + candidateLength)
            {
                var rangeStart = absoluteStart - cumulative;
                var rangeEnd = Math.Clamp(rangeStart + length, rangeStart, candidateLength);
                return (i, rangeStart, rangeEnd);
            }

            cumulative += candidateLength;
        }

        return null;
    }

    private static DocumentOperationValidationResult ApplyInsertBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.Block is null)
        {
            return DocumentOperationValidationResult.Invalid("InsertBlock requires a block payload.");
        }

        var container = FindContainerBlocks(document, operation.Target.TableCellId);
        if (container is null)
        {
            return DocumentOperationValidationResult.Invalid("InsertBlock target table cell was not found.");
        }

        if (container.Any(block => block.Id == operation.Block.Id))
        {
            return DocumentOperationValidationResult.Valid();
        }

        var block = Clone(operation.Block);
        if (operation.Target.Order is not null)
        {
            block.Order = operation.Target.Order.Value;
        }

        if (ReferenceEquals(container, document.Blocks))
        {
            document.Blocks.Add(block);
            document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
            return DocumentOperationValidationResult.Valid();
        }

        // Nested containers (table cells) keep list order — mirror the JS index-based insert.
        var index = Math.Clamp((int)(operation.Target.Order ?? container.Count), 0, container.Count);
        container.Insert(index, block);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyDeleteBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        var removed = document.Blocks.RemoveAll(block => block.Id == operation.Target.BlockId);
        if (removed == 0 && FindBlockLocation(document, operation.Target.BlockId, operation.Target.TableCellId) is { } nested)
        {
            nested.Container.RemoveAt(nested.Index);
        }

        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyUpdateBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.Block is null)
        {
            return DocumentOperationValidationResult.Invalid("UpdateBlock requires a block payload.");
        }

        var index = document.Blocks.FindIndex(block => block.Id == operation.Block.Id);
        if (index < 0)
        {
            // Nested containers (table cells): replace the payload in place, list order kept.
            if (FindBlockLocation(document, operation.Block.Id, operation.Target.TableCellId) is { } nested)
            {
                nested.Container[nested.Index] = Clone(operation.Block);
            }

            return DocumentOperationValidationResult.Valid();
        }

        var existing = document.Blocks[index];
        var updated = Clone(operation.Block);
        if (updated.Order == 0 && existing.Order != 0 && operation.Target.Order is null)
        {
            updated.Order = existing.Order;
        }

        if (operation.Target.Order is not null)
        {
            updated.Order = operation.Target.Order.Value;
        }

        document.Blocks[index] = updated;
        document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyMoveDrawingObject(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.NewLayout is null)
        {
            return DocumentOperationValidationResult.Invalid("MoveDrawingObject requires a new layout payload.");
        }

        if (FindDrawingRun(document, operation.Target) is not { } drawing)
        {
            return DocumentOperationValidationResult.Invalid("MoveDrawingObject target drawing was not found.");
        }

        operation.OldLayout ??= Clone(drawing.Run.Layout);
        operation.OldAnchor ??= Clone(drawing.Run.Layout.Anchor);
        if (operation.NewAnchor is not null)
        {
            ApplyAnchor(drawing.Run.Layout.Anchor, operation.NewAnchor);
        }

        drawing.Run.Layout = Clone(operation.NewLayout);
        if (operation.NewAnchor is not null)
        {
            ApplyAnchor(drawing.Run.Layout.Anchor, operation.NewAnchor);
        }

        operation.NewAnchor ??= Clone(drawing.Run.Layout.Anchor);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyCreateRevision(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (operation.Revision is null)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision requires a revision payload.");
        }

        var revision = document.Revisions.FirstOrDefault(item => item.Id == operation.Revision.Id);
        if (revision is null)
        {
            revision = Clone(operation.Revision);
            revision.Action = DocumentRevisionAction.Pending;
            document.Revisions.Add(revision);
        }
        else if (!string.IsNullOrEmpty(operation.Text))
        {
            revision.PayloadJson = string.IsNullOrEmpty(revision.PayloadJson)
                ? operation.Text
                : revision.PayloadJson + operation.Text;
        }

        var result = revision.Type switch
        {
            DocumentRevisionType.Insertion => ApplyPendingInsertionRevision(document, operation, revision),
            DocumentRevisionType.Deletion => ApplyPendingDeletionRevision(document, operation, revision),
            DocumentRevisionType.Formatting => ApplyPendingFormattingRevision(document, operation, revision),
            _ => DocumentOperationValidationResult.Valid()
        };

        return result;
    }

    private static DocumentOperationValidationResult ApplyPendingInsertionRevision(
        DocumentEditorDocument document,
        DocumentOperation operation,
        DocumentRevision revision)
    {
        var block = FindBlock(document, operation.Target.BlockId ?? revision.Range.BlockId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision insertion target block was not found.");
        }

        var inlineIndex = ResolveInlineIndex(inlines, operation.Target);
        var run = EnsureTextRun(inlines, inlineIndex);
        var offset = Math.Clamp(operation.Target.Offset ?? revision.Range.StartOffset ?? run.Text.Length, 0, run.Text.Length);
        var text = operation.Text ?? revision.PayloadJson ?? string.Empty;

        var replacement = new List<InlineContent>();
        AddTextSlice(replacement, run, 0, offset);
        replacement.Add(new TextRun
        {
            Id = string.IsNullOrWhiteSpace(operation.Target.InlineId)
                ? Guid.NewGuid().ToString("N")
                : $"rev-{revision.Id}",
            Text = text,
            Marks = CopyMarks(run.Marks)
                .Where(mark => mark.Type != InlineMarkType.Revision)
                .Append(CreateRevisionMark(revision))
                .ToList()
        });
        AddTextSlice(replacement, run, offset, run.Text.Length);

        inlines.RemoveAt(inlineIndex);
        inlines.InsertRange(inlineIndex, MergeAdjacentTextRuns(replacement));
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyPendingDeletionRevision(
        DocumentEditorDocument document,
        DocumentOperation operation,
        DocumentRevision revision)
    {
        var block = FindBlock(document, operation.Target.BlockId ?? revision.Range.BlockId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision deletion target block was not found.");
        }

        var inlineIndex = ResolveInlineIndex(inlines, operation.Target);
        if (inlineIndex < 0 || inlineIndex >= inlines.Count || inlines[inlineIndex] is not TextRun run)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision deletion target inline was not found.");
        }

        var offset = Math.Clamp(operation.Target.Offset ?? revision.Range.StartOffset ?? 0, 0, run.Text.Length);
        var length = operation.Target.Length
            ?? Math.Max(0, (revision.Range.EndOffset ?? offset) - (revision.Range.StartOffset ?? offset));
        if (length <= 0)
        {
            length = (operation.Text ?? revision.PayloadJson ?? string.Empty).Length;
        }

        var end = Math.Clamp(offset + length, offset, run.Text.Length);
        if (end <= offset)
        {
            return DocumentOperationValidationResult.Valid();
        }

        var deletedText = operation.Text ?? run.Text[offset..end];
        var replacement = new List<InlineContent>();
        AddTextSlice(replacement, run, 0, offset);
        replacement.Add(new TextRun
        {
            Id = $"rev-{revision.Id}",
            Text = deletedText,
            Marks = CopyMarks(run.Marks)
                .Where(mark => mark.Type != InlineMarkType.Revision)
                .Append(CreateRevisionMark(revision))
                .ToList()
        });
        AddTextSlice(replacement, run, end, run.Text.Length);

        inlines.RemoveAt(inlineIndex);
        inlines.InsertRange(inlineIndex, MergeAdjacentTextRuns(replacement));
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyPendingFormattingRevision(
        DocumentEditorDocument document,
        DocumentOperation operation,
        DocumentRevision revision)
    {
        var block = FindBlock(document, operation.Target.BlockId ?? revision.Range.BlockId);
        var inlines = GetInlineList(block?.Content);
        if (block is null || inlines is null)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision formatting target block was not found.");
        }

        var inlineIndex = ResolveInlineIndex(inlines, operation.Target);
        if (inlineIndex < 0 || inlineIndex >= inlines.Count || inlines[inlineIndex] is not TextRun run)
        {
            return DocumentOperationValidationResult.Invalid("CreateRevision formatting target inline was not found.");
        }

        var payload = ReadJsonValue<DocumentFormattingRevisionPayload>(revision.PayloadJson)
            ?? new DocumentFormattingRevisionPayload
            {
                MarkType = operation.Mark?.Type ?? InlineMarkType.Bold,
                NewActive = true
            };
        var offset = Math.Clamp(operation.Target.Offset ?? revision.Range.StartOffset ?? 0, 0, run.Text.Length);
        var length = operation.Target.Length
            ?? Math.Max(0, (revision.Range.EndOffset ?? offset) - (revision.Range.StartOffset ?? offset));
        var end = Math.Clamp(offset + length, offset, run.Text.Length);
        if (end <= offset)
        {
            return DocumentOperationValidationResult.Valid();
        }

        var replacement = new List<InlineContent>();
        AddTextSlice(replacement, run, 0, offset);
        var marked = SplitInline(run, offset, end);
        marked.Marks.RemoveAll(mark => mark.Type == InlineMarkType.Revision);
        if (payload.NewActive)
        {
            if (!marked.Marks.Any(mark => mark.Type == payload.MarkType))
            {
                marked.Marks.Add(new InlineMark { Type = payload.MarkType });
            }
        }
        else
        {
            marked.Marks.RemoveAll(mark => mark.Type == payload.MarkType);
        }
        marked.Marks.Add(CreateRevisionMark(revision));
        replacement.Add(marked);
        AddTextSlice(replacement, run, end, run.Text.Length);

        inlines.RemoveAt(inlineIndex);
        inlines.InsertRange(inlineIndex, MergeAdjacentTextRuns(replacement));
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyReviewRevision(
        DocumentEditorDocument document,
        DocumentOperation operation,
        DocumentRevisionAction action)
    {
        var revisionId = operation.Revision?.Id ?? operation.Metadata.RevisionId;
        if (string.IsNullOrWhiteSpace(revisionId))
        {
            return DocumentOperationValidationResult.Invalid("Revision review operation requires a revision id.");
        }

        var revision = document.Revisions.FirstOrDefault(item => item.Id == revisionId);
        if (revision is null)
        {
            return DocumentOperationValidationResult.Valid();
        }

        if (revision.Action != DocumentRevisionAction.Pending)
        {
            return DocumentOperationValidationResult.Valid();
        }

        var removeContent = (revision.Type == DocumentRevisionType.Insertion && action == DocumentRevisionAction.Rejected)
            || (revision.Type == DocumentRevisionType.Deletion && action == DocumentRevisionAction.Accepted);

        if (revision.Type == DocumentRevisionType.Formatting)
        {
            ApplyFormattingRevisionDecision(document, revision, action);
        }
        else if (removeContent)
        {
            RemoveRevisionContent(document, revisionId);
        }
        else
        {
            RemoveRevisionMarks(document, revisionId);
        }

        revision.Action = action;
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplyMoveBlock(DocumentEditorDocument document, DocumentOperation operation)
    {
        var location = FindBlockLocation(document, operation.Target.BlockId, operation.Target.TableCellId);
        if (location is null || operation.Target.Order is null)
        {
            return DocumentOperationValidationResult.Invalid("MoveBlock requires an existing block and target order.");
        }

        if (ReferenceEquals(location.Container, document.Blocks))
        {
            // Order-value semantics with a deterministic tie-break: the moved block sorts BEFORE
            // blocks that already carry the same order — mirrors the JS collaboration applier, so
            // "move to order 0" puts the block first on both sides.
            document.Blocks.RemoveAt(location.Index);
            location.Block.Order = operation.Target.Order.Value;
            var bodyIndex = document.Blocks.FindIndex(item => item.Order >= location.Block.Order);
            document.Blocks.Insert(bodyIndex < 0 ? document.Blocks.Count : bodyIndex, location.Block);
            return DocumentOperationValidationResult.Valid();
        }

        // Nested containers (table cells) keep list order — mirror the JS index-based move.
        location.Container.RemoveAt(location.Index);
        var index = Math.Clamp((int)operation.Target.Order.Value, 0, location.Container.Count);
        location.Container.Insert(index, location.Block);
        return DocumentOperationValidationResult.Valid();
    }

    private static DocumentOperationValidationResult ApplySetAttribute(DocumentEditorDocument document, DocumentOperation operation)
    {
        if (string.Equals(operation.AttributeName, "metadata.title", StringComparison.OrdinalIgnoreCase))
        {
            document.Metadata.Title = ReadJsonValue<string>(operation.AttributeValueJson) ?? string.Empty;
            return DocumentOperationValidationResult.Valid();
        }

        var block = FindBlock(document, operation.Target.BlockId, operation.Target.TableCellId);
        if (block is null && string.Equals(operation.AttributeName, "table.cell.text", StringComparison.OrdinalIgnoreCase))
        {
            // 'table.cell.text' targets the TABLE block itself and uses TableCellId to address
            // the cell inside it — the cell id must not act as a container preference here.
            block = FindBlock(document, operation.Target.BlockId);
        }

        if (block is null)
        {
            return DocumentOperationValidationResult.Invalid("SetBlockAttribute target block was not found.");
        }

        if (string.Equals(operation.AttributeName, "text", StringComparison.OrdinalIgnoreCase))
        {
            SetBlockText(block, ReadJsonValue<string>(operation.AttributeValueJson) ?? string.Empty);
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "headingLevel", StringComparison.OrdinalIgnoreCase))
        {
            SetHeadingLevel(block, ReadJsonValue<int>(operation.AttributeValueJson));
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "paragraphProperties", StringComparison.OrdinalIgnoreCase))
        {
            var patch = ReadJsonValue<DocumentParagraphPropertiesPatch>(operation.AttributeValueJson);
            if (patch is null)
            {
                return DocumentOperationValidationResult.Invalid("Paragraph properties payload is missing.");
            }

            ApplyParagraphPropertiesPatch(block.ParagraphProperties, patch);
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "clearFormatting", StringComparison.OrdinalIgnoreCase))
        {
            ApplyClearFormattingRange(block, operation);
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "table.cell.text", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(operation.Target.TableCellId)
                || block.Content is not TableBlockContent table
                || FindTableCell(table, operation.Target.TableCellId) is not { } cell)
            {
                return DocumentOperationValidationResult.Invalid("Table cell text target was not found.");
            }

            SetTableCellText(cell, ReadJsonValue<string>(operation.AttributeValueJson) ?? string.Empty);
            return DocumentOperationValidationResult.Valid();
        }

        if (string.Equals(operation.AttributeName, "order", StringComparison.OrdinalIgnoreCase))
        {
            block.Order = ReadJsonValue<double>(operation.AttributeValueJson);
            document.Blocks = document.Blocks.OrderBy(item => item.Order).ToList();
            return DocumentOperationValidationResult.Valid();
        }

        return DocumentOperationValidationResult.Invalid($"Unsupported attribute '{operation.AttributeName}'.");
    }

    private static void ApplyParagraphPropertiesPatch(
        DocumentParagraphProperties properties,
        DocumentParagraphPropertiesPatch patch)
    {
        if (patch.Alignment is not null)
        {
            properties.Alignment = patch.Alignment.Value;
        }

        if (patch.LineSpacing is not null)
        {
            properties.LineSpacing = Math.Clamp(patch.LineSpacing.Value, 0.5, 4);
        }

        if (patch.SpacingBefore is not null)
        {
            properties.SpacingBefore = Math.Clamp(patch.SpacingBefore.Value, 0, 144);
        }

        if (patch.SpacingAfter is not null)
        {
            properties.SpacingAfter = Math.Clamp(patch.SpacingAfter.Value, 0, 144);
        }

        if (patch.LeftIndent is not null)
        {
            properties.LeftIndent = Math.Clamp(patch.LeftIndent.Value, 0, 432);
        }

        if (patch.RightIndent is not null)
        {
            properties.RightIndent = Math.Clamp(patch.RightIndent.Value, 0, 432);
        }

        if (patch.FirstLineIndent is not null)
        {
            properties.FirstLineIndent = Math.Clamp(patch.FirstLineIndent.Value, -216, 216);
        }

        if (patch.LeftIndentDelta is not null)
        {
            properties.LeftIndent = Math.Clamp(properties.LeftIndent + patch.LeftIndentDelta.Value, 0, 432);
        }

        if (patch.RightIndentDelta is not null)
        {
            properties.RightIndent = Math.Clamp(properties.RightIndent + patch.RightIndentDelta.Value, 0, 432);
        }

        if (patch.FirstLineIndentDelta is not null)
        {
            properties.FirstLineIndent = Math.Clamp(properties.FirstLineIndent + patch.FirstLineIndentDelta.Value, -216, 216);
        }
    }

    private static void ApplyClearFormattingRange(DocumentBlock block, DocumentOperation operation)
    {
        var inlines = GetInlineList(block.Content);
        var inlineIndex = inlines is null ? -1 : ResolveInlineIndex(inlines, operation.Target);
        if (inlines is null || inlineIndex < 0 || inlineIndex >= inlines.Count)
        {
            return;
        }

        var range = ResolveMarkRange(inlines, operation, inlineIndex);
        if (range is null)
        {
            return;
        }

        var (targetInlineIndex, rangeStart, rangeEnd) = range.Value;
        var inline = inlines[targetInlineIndex];
        var text = GetInlineText(inline);
        if (rangeEnd <= rangeStart)
        {
            return;
        }

        var replacement = new List<InlineContent>();
        if (rangeStart > 0)
        {
            replacement.Add(SplitInline(inline, 0, rangeStart));
        }

        var cleared = SplitInline(inline, rangeStart, rangeEnd);
        cleared.Marks.RemoveAll(IsFormattingMark);
        replacement.Add(cleared);

        if (rangeEnd < text.Length)
        {
            replacement.Add(SplitInline(inline, rangeEnd, text.Length));
        }

        inlines.RemoveAt(targetInlineIndex);
        inlines.InsertRange(targetInlineIndex, replacement);
    }

    private static bool IsFormattingMark(InlineMark mark)
    {
        return mark.Type is InlineMarkType.Bold
            or InlineMarkType.Italic
            or InlineMarkType.Underline
            or InlineMarkType.Strikethrough
            or InlineMarkType.Superscript
            or InlineMarkType.Subscript
            or InlineMarkType.SmallCaps
            or InlineMarkType.AllCaps
            or InlineMarkType.DoubleStrikethrough
            or InlineMarkType.CharacterSpacing
            or InlineMarkType.CharacterScale
            or InlineMarkType.Kerning
            or InlineMarkType.Highlight
            or InlineMarkType.TextColor
            or InlineMarkType.FontFamily
            or InlineMarkType.FontSize
            or InlineMarkType.Link;
    }

    private static DocumentBlock? FindBlock(DocumentEditorDocument document, string? blockId, string? preferredCellId = null)
        => FindBlockLocation(document, blockId, preferredCellId)?.Block;

    private sealed record BlockLocation(List<DocumentBlock> Container, DocumentBlock Block, int Index);

    /// <summary>
    /// Deep block resolution mirroring the JS collaboration applier (transform.mjs
    /// findBlockLocation): body blocks first, then recursively through table cells AND
    /// content-control children (template sections); the preferred table cell id, when supplied,
    /// filters which container may match.
    /// </summary>
    private static BlockLocation? FindBlockLocation(DocumentEditorDocument document, string? blockId, string? preferredCellId = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        return Visit(document.Blocks, string.Empty);

        BlockLocation? Visit(List<DocumentBlock> blocks, string cellId)
        {
            for (var index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                if (string.Equals(block.Id, blockId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(preferredCellId) || string.Equals(preferredCellId, cellId, StringComparison.Ordinal)))
                {
                    return new BlockLocation(blocks, block, index);
                }

                if (block.Content is TableBlockContent table)
                {
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
                else if (block.Content is ContentControlBlockContent control)
                {
                    // Content-control children keep the enclosing cell context — a control inside
                    // a table cell still matches that cell's preference.
                    if (Visit(control.Blocks, cellId) is { } nestedInControl)
                    {
                        return nestedInControl;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Resolves the block container an operation addresses: the body when no table cell id is
    /// given, otherwise the blocks of that table cell (mirrors transform.mjs findContainer).
    /// </summary>
    private static List<DocumentBlock>? FindContainerBlocks(DocumentEditorDocument document, string? tableCellId)
    {
        if (string.IsNullOrWhiteSpace(tableCellId))
        {
            return document.Blocks;
        }

        foreach (var block in EnumerateBlocks(document))
        {
            if (block.Content is TableBlockContent table && FindTableCell(table, tableCellId!) is { } cell)
            {
                return cell.Blocks;
            }
        }

        return null;
    }

    private static TableCellContent? FindTableCell(TableBlockContent table, string cellId)
    {
        return table.Rows
            .SelectMany(row => row.Cells)
            .FirstOrDefault(cell => string.Equals(cell.Id, cellId, StringComparison.Ordinal));
    }

    private sealed record DrawingRunTarget(DocumentBlock Block, DocumentDrawingRun Run, int InlineIndex);

    private static DrawingRunTarget? FindDrawingRun(DocumentEditorDocument document, DocumentOperationTarget target)
    {
        foreach (var block in EnumerateBlocks(document))
        {
            if (GetInlineList(block.Content) is not { } inlines)
            {
                continue;
            }

            for (var index = 0; index < inlines.Count; index++)
            {
                if (inlines[index] is not DocumentDrawingRun drawing)
                {
                    continue;
                }

                var objectMatches = !string.IsNullOrWhiteSpace(target.ObjectId)
                    && string.Equals(drawing.ObjectId, target.ObjectId, StringComparison.Ordinal);
                var inlineMatches = !string.IsNullOrWhiteSpace(target.InlineId)
                    && string.Equals(drawing.Id, target.InlineId, StringComparison.Ordinal);
                var indexMatches = string.Equals(block.Id, target.BlockId, StringComparison.Ordinal)
                    && target.InlineIndex == index;
                if (objectMatches || inlineMatches || indexMatches)
                {
                    return new DrawingRunTarget(block, drawing, index);
                }
            }
        }

        return null;
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(DocumentEditorDocument document)
    {
        foreach (var block in EnumerateBlocks(document.Blocks))
        {
            yield return block;
        }

        foreach (var region in document.HeadersFooters)
        {
            foreach (var block in EnumerateBlocks(region.Blocks))
            {
                yield return block;
            }
        }
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block.Content is TableBlockContent table)
            {
                foreach (var cellBlock in table.Rows
                             .SelectMany(row => row.Cells)
                             .SelectMany(cell => EnumerateBlocks(cell.Blocks)))
                {
                    yield return cellBlock;
                }
            }
            else if (block.Content is ContentControlBlockContent control)
            {
                foreach (var controlBlock in EnumerateBlocks(control.Blocks))
                {
                    yield return controlBlock;
                }
            }
        }
    }

    private static void ApplyAnchor(DocumentObjectAnchor target, DocumentObjectAnchor source)
    {
        target.BlockId = source.BlockId;
        target.InlineIndex = source.InlineIndex;
        target.Offset = source.Offset;
        target.Region = source.Region;
        target.TableId = source.TableId;
        target.CellId = source.CellId;
        target.HeaderFooterId = source.HeaderFooterId;
        target.MoveWithText = source.MoveWithText;
        target.FixedOnPage = source.FixedOnPage;
        target.LockAnchor = source.LockAnchor;
    }

    private static List<InlineContent>? GetInlineList(DocumentBlockContent? content)
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

    private static TextRun EnsureTextRun(List<InlineContent> inlines, int inlineIndex)
    {
        while (inlines.Count <= inlineIndex)
        {
            inlines.Add(new TextRun());
        }

        if (inlines[inlineIndex] is TextRun run)
        {
            return run;
        }

        run = new TextRun { Text = GetInlineText(inlines[inlineIndex]) };
        inlines[inlineIndex] = run;
        return run;
    }

    private static int ResolveInlineIndex(List<InlineContent> inlines, DocumentOperationTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.InlineId))
        {
            var inlineIndex = inlines.FindIndex(inline => string.Equals(inline.Id, target.InlineId, StringComparison.Ordinal));
            if (inlineIndex >= 0)
            {
                return inlineIndex;
            }
        }

        return target.InlineIndex ?? 0;
    }

    private static void SetBlockText(DocumentBlock block, string text)
    {
        var inlines = GetInlineList(block.Content);
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();
        inlines.Add(new TextRun { Text = text });
    }

    private static void SetHeadingLevel(DocumentBlock block, int level)
    {
        var clampedLevel = Math.Clamp(level, 1, 6);
        var inlines = GetInlineList(block.Content)?.Select(Clone).ToList() ?? [];
        block.Type = DocumentBlockType.Heading;
        block.Content = new HeadingBlockContent
        {
            Level = clampedLevel,
            Inlines = inlines
        };
    }

    private static void SetTableCellText(TableCellContent cell, string text)
    {
        var paragraph = cell.Blocks
            .FirstOrDefault(block => block.Content is ParagraphBlockContent)
            ?? cell.Blocks.FirstOrDefault();

        if (paragraph is null)
        {
            paragraph = new DocumentBlock
            {
                // Deterministic id: replicas applying the same table.cell.text operation on an
                // empty cell must create the SAME block (mirrored by the JS collab applier).
                Id = $"{cell.Id}-text",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent()
            };
            cell.Blocks.Add(paragraph);
        }

        if (paragraph.Content is not ParagraphBlockContent)
        {
            paragraph.Type = DocumentBlockType.Paragraph;
            paragraph.Content = new ParagraphBlockContent();
        }

        SetBlockText(paragraph, text);
    }

    private static void RemoveRevisionContent(DocumentEditorDocument document, string revisionId)
    {
        foreach (var inlines in GetAllEditableInlineLists(document))
        {
            inlines.RemoveAll(inline => HasRevisionMark(inline, revisionId));
            EnsureEditableInlinesHaveText(inlines);
        }
    }

    private static void ApplyFormattingRevisionDecision(
        DocumentEditorDocument document,
        DocumentRevision revision,
        DocumentRevisionAction action)
    {
        var payload = ReadJsonValue<DocumentFormattingRevisionPayload>(revision.PayloadJson)
            ?? new DocumentFormattingRevisionPayload { MarkType = InlineMarkType.Bold, NewActive = true };
        foreach (var inlines in GetAllEditableInlineLists(document))
        {
            foreach (var inline in inlines.Where(inline => HasRevisionMark(inline, revision.Id)))
            {
                inline.Marks.RemoveAll(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == revision.Id);
                if (action != DocumentRevisionAction.Rejected)
                {
                    continue;
                }

                if (payload.NewActive)
                {
                    inline.Marks.RemoveAll(mark => mark.Type == payload.MarkType);
                }
                else if (!inline.Marks.Any(mark => mark.Type == payload.MarkType))
                {
                    inline.Marks.Add(new InlineMark { Type = payload.MarkType });
                }
            }

            EnsureEditableInlinesHaveText(inlines);
        }
    }

    private static void RemoveRevisionMarks(DocumentEditorDocument document, string revisionId)
    {
        foreach (var inlines in GetAllEditableInlineLists(document))
        {
            foreach (var inline in inlines)
            {
                inline.Marks.RemoveAll(mark =>
                    mark.Type == InlineMarkType.Revision
                    && string.Equals(mark.RevisionId, revisionId, StringComparison.Ordinal));
            }

            EnsureEditableInlinesHaveText(inlines);
        }
    }

    private static IEnumerable<List<InlineContent>> GetAllEditableInlineLists(DocumentEditorDocument document)
    {
        foreach (var block in document.Blocks)
        {
            foreach (var list in GetEditableInlineLists(block))
            {
                yield return list;
            }
        }
    }

    private static IEnumerable<List<InlineContent>> GetEditableInlineLists(DocumentBlock block)
    {
        var list = GetInlineList(block.Content);
        if (list is not null)
        {
            yield return list;
        }

        if (block.Content is TableBlockContent table)
        {
            foreach (var nested in table.Rows
                         .SelectMany(row => row.Cells)
                         .SelectMany(cell => cell.Blocks)
                         .SelectMany(GetEditableInlineLists))
            {
                yield return nested;
            }
        }
    }

    private static bool HasRevisionMark(InlineContent inline, string revisionId)
    {
        return inline.Marks.Any(mark =>
            mark.Type == InlineMarkType.Revision
            && string.Equals(mark.RevisionId, revisionId, StringComparison.Ordinal));
    }

    private static void EnsureEditableInlinesHaveText(List<InlineContent> inlines)
    {
        if (inlines.Count == 0)
        {
            inlines.Add(new TextRun());
        }
    }

    private static void AddTextSlice(List<InlineContent> target, TextRun source, int start, int end)
    {
        var rangeStart = Math.Clamp(start, 0, source.Text.Length);
        var rangeEnd = Math.Clamp(end, rangeStart, source.Text.Length);
        if (rangeEnd <= rangeStart)
        {
            return;
        }

        target.Add(new TextRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = source.Text[rangeStart..rangeEnd],
            Marks = CopyMarks(source.Marks)
        });
    }

    private static List<InlineMark> CopyMarks(IEnumerable<InlineMark> marks)
        => NormalizeMarks(marks);

    private static List<InlineContent> MergeAdjacentTextRuns(IEnumerable<InlineContent> inlines)
    {
        var result = new List<InlineContent>();
        foreach (var inline in inlines)
        {
            if (inline is TextRun run && run.Text.Length == 0)
            {
                continue;
            }

            if (inline is TextRun current
                && result.LastOrDefault() is TextRun previous
                && MarksEqual(previous.Marks, current.Marks))
            {
                previous.Text += current.Text;
                previous.Marks = NormalizeMarks(previous.Marks);
                continue;
            }

            var cloned = Clone(inline);
            cloned.Marks = NormalizeMarks(cloned.Marks);
            result.Add(cloned);
        }

        if (result.Count == 0)
        {
            result.Add(new TextRun());
        }

        return result;
    }

    private static InlineMark CreateRevisionMark(DocumentRevision revision)
        => new()
        {
            Type = InlineMarkType.Revision,
            RevisionId = revision.Id,
            Value = revision.Type.ToString()
        };

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

    private static InlineContent SplitInline(InlineContent inline, int start, int end)
    {
        var text = GetInlineText(inline);
        var rangeStart = ClampTextBoundary(text, start, isEnd: false);
        var rangeEnd = ClampTextBoundary(text, end, isEnd: true);
        if (rangeEnd < rangeStart)
        {
            rangeEnd = rangeStart;
        }

        var length = Math.Min(rangeEnd, text.Length) - rangeStart;
        length = Math.Max(length, 0);
        var slice = length > 0 ? text.Substring(rangeStart, length) : string.Empty;

        var cloned = Clone(inline);
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

    private static int ClampTextBoundary(string text, int index, bool isEnd)
    {
        var value = Math.Clamp(index, 0, text.Length);
        if (value > 0
            && value < text.Length
            && char.IsHighSurrogate(text[value - 1])
            && char.IsLowSurrogate(text[value]))
        {
            return isEnd ? value + 1 : value - 1;
        }

        return value;
    }

    private static T? ReadJsonValue<T>(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options);
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

    private static bool MarksEqual(IReadOnlyList<InlineMark> left, IReadOnlyList<InlineMark> right)
    {
        var normalizedLeft = NormalizeMarks(left);
        var normalizedRight = NormalizeMarks(right);
        return normalizedLeft.Count == normalizedRight.Count
            && normalizedLeft.Zip(normalizedRight).All(pair => SameMark(pair.First, pair.Second));
    }

    private static List<InlineMark> NormalizeMarks(IEnumerable<InlineMark> marks)
        => marks
            .Select(Clone)
            .GroupBy(MarkComparisonKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(MarkComparisonKey, StringComparer.Ordinal)
            .ToList();

    private static string MarkComparisonKey(InlineMark mark)
        => string.Join("\u001f",
            ((int)mark.Type).ToString(),
            mark.Value ?? string.Empty,
            mark.Link?.Href ?? string.Empty,
            mark.Link?.Title ?? string.Empty,
            mark.CommentAnchor?.CommentId ?? string.Empty,
            mark.CommentAnchor?.AnchorId ?? string.Empty,
            mark.RevisionId ?? string.Empty);

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
