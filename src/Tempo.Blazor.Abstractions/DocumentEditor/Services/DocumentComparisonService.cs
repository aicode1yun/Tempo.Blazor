using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Compares document snapshots without relying on version history.</summary>
public sealed class DocumentComparisonService
{
    /// <summary>Compares two document snapshots.</summary>
    public DocumentCompareResult Compare(DocumentEditorDocument baseDocument, DocumentEditorDocument compareDocument)
    {
        var result = new DocumentCompareResult
        {
            BaseDocument = Clone(baseDocument),
            CompareDocument = Clone(compareDocument),
            TextDiff = DocumentTextDiffHelper.Diff(
                DocumentTextDiffHelper.ExtractPlainText(baseDocument),
                DocumentTextDiffHelper.ExtractPlainText(compareDocument))
        };

        var baseBlocks = baseDocument.Blocks.OrderBy(block => block.Order).ToList();
        var compareBlocks = compareDocument.Blocks.OrderBy(block => block.Order).ToList();
        var compareById = compareBlocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
        var matchedCompareIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var baseBlock in baseBlocks)
        {
            var oldText = DocumentTextDiffHelper.ExtractBlockPlainText(baseBlock);
            if (!compareById.TryGetValue(baseBlock.Id, out var compareBlock))
            {
                result.Changes.Add(new DocumentCompareBlockChange
                {
                    Kind = DocumentCompareChangeKind.Removed,
                    BlockId = baseBlock.Id,
                    OldText = oldText,
                    TextDiff = DocumentTextDiffHelper.Diff(oldText, string.Empty)
                });
                continue;
            }

            matchedCompareIds.Add(compareBlock.Id);
            var newText = DocumentTextDiffHelper.ExtractBlockPlainText(compareBlock);
            if (!string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                result.Changes.Add(new DocumentCompareBlockChange
                {
                    Kind = DocumentCompareChangeKind.Changed,
                    BlockId = baseBlock.Id,
                    OldText = oldText,
                    NewText = newText,
                    TextDiff = DocumentTextDiffHelper.Diff(oldText, newText)
                });
            }
        }

        foreach (var compareBlock in compareBlocks.Where(block => !matchedCompareIds.Contains(block.Id)))
        {
            var newText = DocumentTextDiffHelper.ExtractBlockPlainText(compareBlock);
            result.Changes.Add(new DocumentCompareBlockChange
            {
                Kind = DocumentCompareChangeKind.Added,
                BlockId = compareBlock.Id,
                NewText = newText,
                TextDiff = DocumentTextDiffHelper.Diff(string.Empty, newText)
            });
        }

        result.Summary = new DocumentCompareSummary
        {
            AddedBlocks = result.Changes.Count(change => change.Kind == DocumentCompareChangeKind.Added),
            RemovedBlocks = result.Changes.Count(change => change.Kind == DocumentCompareChangeKind.Removed),
            ChangedBlocks = result.Changes.Count(change => change.Kind == DocumentCompareChangeKind.Changed)
        };

        return result;
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document comparison value.");
    }
}
