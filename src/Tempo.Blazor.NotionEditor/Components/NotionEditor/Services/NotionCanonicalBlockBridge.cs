using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionCanonicalBlockBridge
{
    public static NotionPageSnapshot InsertBlocks(
        NotionPageSnapshot snapshot,
        IReadOnlyList<IPageBlock> inserted,
        Guid? afterBlockId)
    {
        var insertedIds = inserted.Select(block => block.Id).ToHashSet();
        var roots = inserted
            .Where(block => block.ParentBlockId is null)
            .OrderBy(block => block.Order)
            .ToList();
        var existingRoots = snapshot.Blocks
            .Where(block => block.ParentBlockId is null && !insertedIds.Contains(block.Id))
            .OrderBy(block => block.Order)
            .ThenBy(block => block.Id)
            .ToList();
        var afterIndex = afterBlockId is { } id
            ? existingRoots.FindIndex(block => block.Id == id)
            : -1;
        var insertionIndex = afterIndex >= 0
            ? afterIndex + 1
            : existingRoots.Count;
        insertionIndex = Math.Clamp(insertionIndex, 0, existingRoots.Count);

        var combinedRoots = existingRoots.ToList();
        combinedRoots.InsertRange(
            insertionIndex,
            roots.Select(ToSnapshot));
        for (var index = 0; index < combinedRoots.Count; index++)
        {
            combinedRoots[index].Order = index;
        }

        var retainedChildren = snapshot.Blocks
            .Where(block => block.ParentBlockId is not null && !insertedIds.Contains(block.Id))
            .ToList();
        var insertedChildren = inserted
            .Where(block => block.ParentBlockId is not null)
            .Select(ToSnapshot)
            .ToList();
        var combined = combinedRoots
            .Concat(retainedChildren)
            .Concat(insertedChildren)
            .ToList();
        foreach (var siblings in combined.GroupBy(block => block.ParentBlockId))
        {
            var order = 0;
            foreach (var sibling in siblings
                         .OrderBy(block => block.Order)
                         .ThenBy(block => block.Id))
            {
                sibling.Order = order++;
            }
        }

        snapshot.Blocks = combined
            .OrderBy(block => block.ParentBlockId)
            .ThenBy(block => block.Order)
            .ThenBy(block => block.Id)
            .ToList();
        return snapshot;
    }

    private static NotionBlockSnapshot ToSnapshot(IPageBlock block)
        => new()
        {
            Id = block.Id,
            PageId = block.PageId,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = block.Order,
            CreatedAt = block.CreatedAt,
            LastEditedAt = block.LastEditedAt,
            Content = block.Type switch
            {
                BlockType.Table => JsonSerializer.SerializeToElement(
                    NotionCanonicalTableBridge.ToCanonicalTable(
                        (ITableBlockContent)block.Content),
                    NotionAggregateJson.Options),
                BlockType.TableRow => JsonSerializer.SerializeToElement(
                    NotionCanonicalTableBridge.ToCanonicalRow(
                        (ITableRowBlockContent)block.Content),
                    NotionAggregateJson.Options),
                _ => JsonSerializer.SerializeToElement(
                    block.Content,
                    block.Content.GetType(),
                    NotionAggregateJson.Options)
            }
        };
}
