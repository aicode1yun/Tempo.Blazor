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
        var deletedTextRanges = new HashSet<string>(StringComparer.Ordinal);

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

            if (operation.Type is DocumentOperationType.AddMark or DocumentOperationType.RemoveMark
                && IsCoveredByDelete(operation, result.Where(item => item.Type == DocumentOperationType.DeleteText)))
            {
                continue;
            }

            result.Add(operation);
        }

        return result;
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
