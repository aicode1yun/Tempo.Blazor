using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Deterministic conflict resolver for the first document operation prototype.</summary>
public class DocumentOperationConflictResolver
{
    /// <summary>Orders and transforms concurrent operations into a deterministic sequence.</summary>
    public IReadOnlyList<DocumentOperation> Resolve(IEnumerable<DocumentOperation> operations)
    {
        var ordered = operations
            .Select(Clone)
            .OrderBy(GetOrderKey, StringComparer.Ordinal)
            .ToList();

        var result = new List<DocumentOperation>();
        var deletedBlocks = new HashSet<string>(StringComparer.Ordinal);
        var finalBlockMoves = GetFinalOperationKeys(ordered, DocumentOperationType.MoveBlock, op => op.Target.BlockId);
        var finalTextAttributes = GetFinalOperationKeys(ordered, DocumentOperationType.SetBlockAttribute, op => $"{op.Target.BlockId}:{op.AttributeName}");
        // Object operations resolve last-write-wins per stable identity (same rule the block
        // moves/attributes already use): concurrent drawing moves keep the final position, and
        // concurrent whole-block updates keep the final payload.
        var finalObjectMoves = GetFinalOperationKeys(ordered, DocumentOperationType.MoveDrawingObject, op => op.Target.ObjectId);
        var finalBlockUpdates = GetFinalOperationKeys(ordered, DocumentOperationType.UpdateBlock, op => op.Target.BlockId);
        var deletedTextRanges = new HashSet<string>(StringComparer.Ordinal);
        // Revision decisions resolve first-decision-wins: once a revision is accepted or rejected,
        // a concurrent opposite decision is a no-op.
        var decidedRevisions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var operation in ordered)
        {
            if (!string.IsNullOrWhiteSpace(operation.Target.BlockId) && deletedBlocks.Contains(operation.Target.BlockId!))
            {
                continue;
            }

            if (operation.Type == DocumentOperationType.DeleteBlock && !string.IsNullOrWhiteSpace(operation.Target.BlockId))
            {
                deletedBlocks.Add(operation.Target.BlockId!);
                result.RemoveAll(existing => existing.Target.BlockId == operation.Target.BlockId);
                result.Add(operation);
                continue;
            }

            if (operation.Type == DocumentOperationType.MoveBlock
                && finalBlockMoves.TryGetValue(operation.Target.BlockId ?? string.Empty, out var moveId)
                && moveId != operation.OperationId)
            {
                continue;
            }

            if (operation.Type == DocumentOperationType.SetBlockAttribute
                && finalTextAttributes.TryGetValue($"{operation.Target.BlockId}:{operation.AttributeName}", out var setId)
                && setId != operation.OperationId)
            {
                continue;
            }

            if (operation.Type == DocumentOperationType.DeleteText)
            {
                var rangeKey = GetTextRangeKey(operation);
                if (!deletedTextRanges.Add(rangeKey))
                {
                    continue;
                }

                result.Add(operation);
                continue;
            }

            if (operation.Type == DocumentOperationType.InsertText)
            {
                TransformInsertAgainstDeletes(operation, result.Where(item => item.Type == DocumentOperationType.DeleteText));
                TransformInsertAgainstPriorInserts(operation, result.Where(item => item.Type == DocumentOperationType.InsertText));
            }

            if (operation.Type == DocumentOperationType.MoveDrawingObject
                && finalObjectMoves.TryGetValue(operation.Target.ObjectId ?? string.Empty, out var objectMoveId)
                && objectMoveId != operation.OperationId)
            {
                continue;
            }

            if (operation.Type == DocumentOperationType.UpdateBlock
                && finalBlockUpdates.TryGetValue(operation.Target.BlockId ?? string.Empty, out var updateId)
                && updateId != operation.OperationId)
            {
                continue;
            }

            if (operation.Type is DocumentOperationType.AcceptRevision or DocumentOperationType.RejectRevision)
            {
                var revisionId = operation.Revision?.Id ?? string.Empty;
                if (revisionId.Length > 0 && !decidedRevisions.Add(revisionId))
                {
                    continue;
                }
            }

            if (operation.Type is DocumentOperationType.AddMark or DocumentOperationType.RemoveMark)
            {
                // Formatting ranges live in the same text coordinate space as inserts, so they
                // must transform against every concurrent text edit that precedes them.
                if (!TransformMarkRange(operation, result))
                {
                    continue;
                }
            }

            result.Add(operation);
        }

        return result;
    }

    /// <summary>
    /// Transforms an inline-mark range against prior text edits: inserts before the range shift it
    /// right (inserts inside extend it), deletes shift/clip it. Returns false when the whole range
    /// was deleted and the operation should be dropped.
    /// </summary>
    private static bool TransformMarkRange(DocumentOperation operation, IReadOnlyList<DocumentOperation> priorOperations)
    {
        var offset = operation.Target.Offset ?? 0;
        var length = operation.Target.Length ?? 0;

        foreach (var prior in priorOperations)
        {
            if (!SameTextTarget(operation, prior))
            {
                continue;
            }

            if (prior.Type == DocumentOperationType.InsertText)
            {
                var insertOffset = prior.Target.Offset ?? 0;
                var insertLength = (prior.Text ?? string.Empty).Length;
                if (insertOffset <= offset)
                {
                    offset += insertLength;
                }
                else if (insertOffset < offset + length)
                {
                    length += insertLength;
                }
            }
            else if (prior.Type == DocumentOperationType.DeleteText)
            {
                var deleteOffset = prior.Target.Offset ?? 0;
                var deleteLength = GetTextLength(prior);
                var deleteEnd = deleteOffset + deleteLength;
                var rangeEnd = offset + length;

                if (deleteEnd <= offset)
                {
                    // Delete entirely before the range.
                    offset -= deleteLength;
                }
                else if (deleteOffset >= rangeEnd)
                {
                    // Delete entirely after the range — no effect.
                }
                else
                {
                    // Overlap: remove the intersected part; when the delete starts before the
                    // range, the surviving range also shifts left to the delete start.
                    var overlapStart = Math.Max(offset, deleteOffset);
                    var overlapEnd = Math.Min(rangeEnd, deleteEnd);
                    length -= overlapEnd - overlapStart;
                    if (deleteOffset < offset)
                    {
                        offset = deleteOffset;
                    }
                }

                if (length <= 0)
                {
                    return false;
                }
            }
        }

        operation.Target.Offset = offset;
        operation.Target.Length = length;
        return true;
    }

    private static void TransformInsertAgainstPriorInserts(DocumentOperation operation, IEnumerable<DocumentOperation> priorInserts)
    {
        foreach (var prior in priorInserts)
        {
            if (!SameTextTarget(operation, prior))
            {
                continue;
            }

            var priorOffset = prior.Target.Offset ?? 0;
            var offset = operation.Target.Offset ?? 0;
            if (priorOffset < offset || (priorOffset == offset && string.Compare(GetOrderKey(prior), GetOrderKey(operation), StringComparison.Ordinal) < 0))
            {
                operation.Target.Offset = offset + (prior.Text ?? string.Empty).Length;
            }
        }
    }

    private static void TransformInsertAgainstDeletes(DocumentOperation operation, IEnumerable<DocumentOperation> deletes)
    {
        foreach (var delete in deletes)
        {
            if (!SameTextTarget(operation, delete))
            {
                continue;
            }

            var deleteOffset = delete.Target.Offset ?? 0;
            var deleteLength = GetTextLength(delete);
            var offset = operation.Target.Offset ?? 0;

            if (offset > deleteOffset + deleteLength)
            {
                operation.Target.Offset = offset - deleteLength;
            }
            else if (offset >= deleteOffset)
            {
                operation.Target.Offset = deleteOffset;
            }
        }
    }

    private static bool IsCoveredByDelete(DocumentOperation operation, IEnumerable<DocumentOperation> deletes)
    {
        var offset = operation.Target.Offset ?? 0;
        return deletes.Any(delete =>
            SameTextTarget(operation, delete)
            && offset >= (delete.Target.Offset ?? 0)
            && offset < (delete.Target.Offset ?? 0) + GetTextLength(delete));
    }

    private static bool SameTextTarget(DocumentOperation left, DocumentOperation right)
    {
        return left.Target.BlockId == right.Target.BlockId
            && (string.IsNullOrWhiteSpace(left.Target.InlineId)
                || string.IsNullOrWhiteSpace(right.Target.InlineId)
                || string.Equals(left.Target.InlineId, right.Target.InlineId, StringComparison.Ordinal))
            && (left.Target.InlineIndex ?? 0) == (right.Target.InlineIndex ?? 0);
    }

    private static string GetTextRangeKey(DocumentOperation operation)
    {
        return $"{operation.Target.BlockId}:{operation.Target.InlineId}:{operation.Target.InlineIndex ?? 0}:{operation.Target.Offset ?? 0}:{GetTextLength(operation)}";
    }

    private static int GetTextLength(DocumentOperation operation)
    {
        return operation.Target.Length ?? (operation.Text ?? string.Empty).Length;
    }

    private static Dictionary<string, string> GetFinalOperationKeys(
        IReadOnlyList<DocumentOperation> operations,
        DocumentOperationType type,
        Func<DocumentOperation, string?> keySelector)
    {
        return operations
            .Where(operation => operation.Type == type)
            .Select(operation => new { Key = keySelector(operation) ?? string.Empty, operation.OperationId })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().OperationId, StringComparer.Ordinal);
    }

    private static string GetOrderKey(DocumentOperation operation)
    {
        return $"{operation.Metadata.LogicalTimestamp:D20}|{operation.Metadata.ClientId}|{operation.Metadata.AuthorId}|{operation.OperationId}";
    }

    private static DocumentOperation Clone(DocumentOperation operation)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(operation, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<DocumentOperation>(json, DocumentEditorJson.Options)!;
    }
}
