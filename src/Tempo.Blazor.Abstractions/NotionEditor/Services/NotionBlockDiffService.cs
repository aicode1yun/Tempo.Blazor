using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Services;

/// <summary>Compares two page block snapshots and returns block-level differences.</summary>
public static class NotionBlockDiffService
{
    public static IReadOnlyList<BlockDiff> Compare(
        IEnumerable<IPageBlock> beforeBlocks,
        IEnumerable<IPageBlock> afterBlocks)
    {
        ArgumentNullException.ThrowIfNull(beforeBlocks);
        ArgumentNullException.ThrowIfNull(afterBlocks);

        var beforeSnapshot = beforeBlocks.ToList();
        var afterSnapshot = afterBlocks.ToList();
        var before = beforeSnapshot.ToDictionary(BlockKey, StringComparer.OrdinalIgnoreCase);
        var after = afterSnapshot.ToDictionary(BlockKey, StringComparer.OrdinalIgnoreCase);
        var diffs = new List<BlockDiff>();

        foreach (var beforeBlock in beforeSnapshot.OrderBy(block => block.Order).ThenBy(block => block.Id))
        {
            var id = BlockKey(beforeBlock);
            if (!after.TryGetValue(id, out var afterBlock))
            {
                diffs.Add(new BlockDiff(id, BlockDiffType.Removed, ToPageBlock(beforeBlock), null));
                continue;
            }

            if (HasContentChange(beforeBlock, afterBlock))
            {
                diffs.Add(new BlockDiff(id, BlockDiffType.Modified, ToPageBlock(beforeBlock), ToPageBlock(afterBlock)));
                continue;
            }

            if (HasMoveChange(beforeBlock, afterBlock))
                diffs.Add(new BlockDiff(id, BlockDiffType.Moved, ToPageBlock(beforeBlock), ToPageBlock(afterBlock)));
        }

        foreach (var afterBlock in afterSnapshot.OrderBy(block => block.Order).ThenBy(block => block.Id))
        {
            var id = BlockKey(afterBlock);
            if (!before.ContainsKey(id))
                diffs.Add(new BlockDiff(id, BlockDiffType.Added, null, ToPageBlock(afterBlock)));
        }

        return diffs;
    }

    private static string BlockKey(IPageBlock block)
        => block.Id.ToString("D");

    private static bool HasMoveChange(IPageBlock before, IPageBlock after)
        => before.Order != after.Order || before.ParentBlockId != after.ParentBlockId;

    private static bool HasContentChange(IPageBlock before, IPageBlock after)
    {
        if (before.Type != after.Type)
            return true;

        return SerializeContent(before.Content) != SerializeContent(after.Content);
    }

    private static string SerializeContent(IBlockContent content)
        => JsonSerializer.Serialize(content, content.GetType());

    private static PageBlock ToPageBlock(IPageBlock block)
        => new()
        {
            Id = block.Id,
            PageId = block.PageId,
            ParentBlockId = block.ParentBlockId,
            Type = block.Type,
            Order = block.Order,
            Content = CloneContent(block.Content),
            CreatedAt = block.CreatedAt,
            LastEditedAt = block.LastEditedAt
        };

    private static IBlockContent CloneContent(IBlockContent content)
    {
        var contentType = content.GetType();
        var json = JsonSerializer.Serialize(content, contentType);
        return (IBlockContent)(JsonSerializer.Deserialize(json, contentType)
            ?? throw new InvalidOperationException($"Unable to clone block content of type {contentType.FullName}."));
    }
}
