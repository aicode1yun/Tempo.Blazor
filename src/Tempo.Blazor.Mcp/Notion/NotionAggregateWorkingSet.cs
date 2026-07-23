using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal sealed class NotionAggregateWorkingSet
{
    private readonly Dictionary<Guid, NotionPageSnapshot> _pages;
    private readonly HashSet<Guid> _touchedPageIds = [];

    public NotionAggregateWorkingSet(IReadOnlyDictionary<Guid, NotionPageSnapshot> pages)
    {
        _pages = pages.ToDictionary(pair => pair.Key, pair => Clone(pair.Value));
    }

    public IReadOnlyDictionary<Guid, NotionPageSnapshot> Pages => _pages;
    public IReadOnlySet<Guid> TouchedPageIds => _touchedPageIds;

    public NotionCanonicalApplyResult UpsertBlock(
        int operationIndex,
        string? clientRef,
        NotionBlockSnapshot source)
    {
        if (!_pages.TryGetValue(source.PageId, out var targetPage))
        {
            return Failure(
                operationIndex,
                "page_not_loaded",
                $"Page '{source.PageId}' was not loaded for this operation.");
        }

        var existing = FindBlock(source.Id);
        if (existing is not null && existing.Value.Page.Page.Id != source.PageId)
        {
            return Failure(
                operationIndex,
                "block_page_mismatch",
                $"Block '{source.Id}' belongs to page '{existing.Value.Page.Page.Id}'; use a move operation.");
        }

        var block = Clone(source);
        var blocks = targetPage.Blocks.ToList();
        var existingIndex = blocks.FindIndex(candidate => candidate.Id == block.Id);
        NotionEntityChange change;
        if (existingIndex >= 0)
        {
            blocks[existingIndex] = block;
            change = new NotionEntityChange(operationIndex, clientRef, source.PageId, source.Id);
            targetPage.Blocks = blocks;
            _touchedPageIds.Add(source.PageId);
            return NotionCanonicalApplyResult.Applied(updated: [change]);
        }

        foreach (var sibling in blocks.Where(candidate =>
                     candidate.ParentBlockId == block.ParentBlockId &&
                     candidate.Order >= block.Order))
        {
            sibling.Order++;
        }
        blocks.Add(block);
        targetPage.Blocks = blocks;
        _touchedPageIds.Add(source.PageId);
        change = new NotionEntityChange(operationIndex, clientRef, source.PageId, source.Id);
        return NotionCanonicalApplyResult.Applied(created: [change]);
    }

    public NotionCanonicalApplyResult DeleteBlock(
        int operationIndex,
        string? clientRef,
        Guid blockId)
    {
        var located = FindBlock(blockId);
        if (located is null)
        {
            return Failure(operationIndex, "block_not_found", $"Block '{blockId}' was not found.");
        }

        var page = located.Value.Page;
        var ids = DescendantIds(page, blockId);
        ids.Add(blockId);
        page.Blocks = page.Blocks.Where(block => !ids.Contains(block.Id)).ToList();
        _touchedPageIds.Add(page.Page.Id);

        var deleted = ids
            .OrderBy(id => id)
            .Select(id => new NotionEntityChange(operationIndex, clientRef, page.Page.Id, id))
            .ToList();
        return NotionCanonicalApplyResult.Applied(deleted: deleted);
    }

    public NotionCanonicalApplyResult MoveBlock(
        int operationIndex,
        string? clientRef,
        Guid blockId,
        Guid sourcePageId,
        Guid targetPageId,
        Guid? targetParentBlockId,
        int targetOrder)
    {
        if (!_pages.TryGetValue(sourcePageId, out var sourcePage))
        {
            return Failure(
                operationIndex,
                "source_page_not_loaded",
                $"Source page '{sourcePageId}' was not loaded.");
        }
        if (!_pages.TryGetValue(targetPageId, out var targetPage))
        {
            return Failure(
                operationIndex,
                "target_page_not_loaded",
                $"Target page '{targetPageId}' was not loaded.");
        }

        var root = sourcePage.Blocks.FirstOrDefault(block => block.Id == blockId);
        if (root is null)
        {
            return Failure(
                operationIndex,
                "block_not_found",
                $"Block '{blockId}' was not found on source page '{sourcePageId}'.");
        }

        var descendantIds = DescendantIds(sourcePage, blockId);
        if (targetParentBlockId == blockId || targetParentBlockId is not null && descendantIds.Contains(targetParentBlockId.Value))
        {
            return Failure(
                operationIndex,
                "invalid_move_target",
                "A block cannot be moved below itself or one of its descendants.");
        }

        if (targetParentBlockId is not null &&
            !targetPage.Blocks.Any(block => block.Id == targetParentBlockId.Value) &&
            !(sourcePageId == targetPageId && descendantIds.Contains(targetParentBlockId.Value)))
        {
            return Failure(
                operationIndex,
                "target_parent_not_found",
                $"Target parent block '{targetParentBlockId}' was not found on page '{targetPageId}'.");
        }

        var movedIds = new HashSet<Guid>(descendantIds) { blockId };
        var moved = sourcePage.Blocks
            .Where(block => movedIds.Contains(block.Id))
            .Select(Clone)
            .ToList();

        sourcePage.Blocks = sourcePage.Blocks.Where(block => !movedIds.Contains(block.Id)).ToList();
        if (sourcePageId != targetPageId)
        {
            targetPage.Blocks = targetPage.Blocks.Where(block => !movedIds.Contains(block.Id)).ToList();
        }

        foreach (var sibling in targetPage.Blocks.Where(block =>
                     block.ParentBlockId == targetParentBlockId &&
                     block.Order >= Math.Max(0, targetOrder)))
        {
            sibling.Order++;
        }
        foreach (var block in moved)
        {
            block.PageId = targetPageId;
            if (block.Id == blockId)
            {
                block.ParentBlockId = targetParentBlockId;
                block.Order = Math.Max(0, targetOrder);
            }
        }

        targetPage.Blocks = targetPage.Blocks.Concat(moved).ToList();
        _touchedPageIds.Add(sourcePageId);
        _touchedPageIds.Add(targetPageId);

        return NotionCanonicalApplyResult.Applied(
            updated:
            [
                new NotionEntityChange(operationIndex, clientRef, targetPageId, blockId)
            ]);
    }

    public NotionCanonicalApplyResult ReplacePage(
        int operationIndex,
        string? clientRef,
        NotionPageState source)
    {
        if (!_pages.TryGetValue(source.Id, out var snapshot))
        {
            return Failure(
                operationIndex,
                "page_not_loaded",
                $"Page '{source.Id}' was not loaded for this operation.");
        }

        snapshot.Page = Clone(source);
        _touchedPageIds.Add(source.Id);
        return NotionCanonicalApplyResult.Applied(
            updated:
            [
                new NotionEntityChange(operationIndex, clientRef, source.Id, source.Id)
            ]);
    }

    public NotionCanonicalApplyResult PatchBlockContent(
        int operationIndex,
        string? clientRef,
        Guid blockId,
        JsonObject patch)
    {
        var located = FindBlock(blockId);
        if (located is null)
        {
            return Failure(operationIndex, "block_not_found", $"Block '{blockId}' was not found.");
        }

        if (JsonNode.Parse(located.Value.Block.Content.GetRawText()) is not JsonObject content)
        {
            return Failure(
                operationIndex,
                "block_content_not_object",
                $"Block '{blockId}' content cannot be patched because it is not a JSON object.");
        }

        ApplyMergePatch(content, patch);
        located.Value.Block.Content = JsonSerializer.SerializeToElement(
            content,
            NotionAggregateJson.Options);
        _touchedPageIds.Add(located.Value.Page.Page.Id);
        return NotionCanonicalApplyResult.Applied(
            updated:
            [
                new NotionEntityChange(
                    operationIndex,
                    clientRef,
                    located.Value.Page.Page.Id,
                    blockId)
            ]);
    }

    public NotionCanonicalApplyResult ConvertBlock(
        int operationIndex,
        string? clientRef,
        Guid blockId,
        BlockType newType,
        JsonElement content)
    {
        var located = FindBlock(blockId);
        if (located is null)
        {
            return Failure(operationIndex, "block_not_found", $"Block '{blockId}' was not found.");
        }

        located.Value.Block.Type = newType;
        located.Value.Block.Content = content.Clone();
        _touchedPageIds.Add(located.Value.Page.Page.Id);
        return NotionCanonicalApplyResult.Applied(
            updated:
            [
                new NotionEntityChange(
                    operationIndex,
                    clientRef,
                    located.Value.Page.Page.Id,
                    blockId)
            ]);
    }

    public NotionCanonicalApplyResult ReorderBlocks(
        int operationIndex,
        string? clientRef,
        Guid pageId,
        Guid? parentBlockId,
        IReadOnlyList<Guid> orderedBlockIds)
    {
        if (!_pages.TryGetValue(pageId, out var page))
        {
            return Failure(
                operationIndex,
                "page_not_loaded",
                $"Page '{pageId}' was not loaded for this operation.");
        }

        var siblings = page.Blocks
            .Where(block => block.ParentBlockId == parentBlockId)
            .ToList();
        if (orderedBlockIds.Count != orderedBlockIds.Distinct().Count())
        {
            return Failure(
                operationIndex,
                "duplicate_reorder_id",
                "orderedBlockIds must not contain duplicates.");
        }
        if (!siblings.Select(block => block.Id).ToHashSet().SetEquals(orderedBlockIds))
        {
            return Failure(
                operationIndex,
                "reorder_set_mismatch",
                "orderedBlockIds must contain every sibling exactly once.");
        }

        var byId = siblings.ToDictionary(block => block.Id);
        for (var order = 0; order < orderedBlockIds.Count; order++)
        {
            byId[orderedBlockIds[order]].Order = order;
        }

        _touchedPageIds.Add(pageId);
        return NotionCanonicalApplyResult.Applied(
            updated: orderedBlockIds
                .Select(id => new NotionEntityChange(operationIndex, clientRef, pageId, id))
                .ToList());
    }

    public bool TryGetBlock(
        Guid blockId,
        out Guid pageId,
        out NotionBlockSnapshot? block)
    {
        var located = FindBlock(blockId);
        if (located is null)
        {
            pageId = Guid.Empty;
            block = null;
            return false;
        }

        pageId = located.Value.Page.Page.Id;
        block = located.Value.Block;
        return true;
    }

    public int GetNextSiblingOrder(Guid pageId, Guid? parentBlockId)
    {
        if (!_pages.TryGetValue(pageId, out var page))
        {
            return 0;
        }

        var siblings = page.Blocks.Where(block => block.ParentBlockId == parentBlockId).ToList();
        return siblings.Count == 0 ? 0 : siblings.Max(block => block.Order) + 1;
    }

    public IReadOnlyList<Guid> GetSiblingBlockIds(Guid pageId, Guid? parentBlockId)
        => _pages.TryGetValue(pageId, out var page)
            ? page.Blocks
                .Where(block => block.ParentBlockId == parentBlockId)
                .OrderBy(block => block.Order)
                .ThenBy(block => block.Id)
                .Select(block => block.Id)
                .ToList()
            : [];

    private (NotionPageSnapshot Page, NotionBlockSnapshot Block)? FindBlock(Guid blockId)
    {
        foreach (var page in _pages.Values)
        {
            var block = page.Blocks.FirstOrDefault(candidate => candidate.Id == blockId);
            if (block is not null)
            {
                return (page, block);
            }
        }

        return null;
    }

    private static HashSet<Guid> DescendantIds(NotionPageSnapshot page, Guid parentId)
    {
        var descendants = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(parentId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in page.Blocks.Where(block => block.ParentBlockId == current))
            {
                if (descendants.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return descendants;
    }

    private static NotionCanonicalApplyResult Failure(
        int operationIndex,
        string code,
        string message)
        => NotionCanonicalApplyResult.Failed(new NotionAggregateIssue
        {
            Code = code,
            Severity = NotionIssueSeverity.Error,
            Message = message,
            Path = $"$.operations[{operationIndex}]"
        });

    private static void ApplyMergePatch(JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is null)
            {
                target.Remove(property.Key);
                continue;
            }

            if (property.Value is JsonObject patchObject &&
                target[property.Key] is JsonObject targetObject)
            {
                ApplyMergePatch(targetObject, patchObject);
                continue;
            }

            target[property.Key] = property.Value.DeepClone();
        }
    }

    internal static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, NotionAggregateJson.Options);
        return JsonSerializer.Deserialize<T>(json, NotionAggregateJson.Options)
            ?? throw new JsonException($"Could not clone {typeof(T).Name}.");
    }
}
