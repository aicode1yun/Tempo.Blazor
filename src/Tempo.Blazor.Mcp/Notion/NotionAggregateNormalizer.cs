using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal static class NotionAggregateNormalizer
{
    public static void Normalize(NotionAggregateWorkingSet workingSet)
    {
        foreach (var pageId in workingSet.TouchedPageIds.OrderBy(id => id))
        {
            var snapshot = workingSet.Pages[pageId];
            var blocks = snapshot.Blocks.ToList();
            var rootBlocks = blocks
                .Where(block => block.ParentBlockId is null)
                .OrderBy(block => block.Order)
                .ThenBy(block => block.Id)
                .ToList();
            var childGroups = blocks
                .Where(block => block.ParentBlockId is not null)
                .GroupBy(block => block.ParentBlockId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(block => block.Order)
                        .ThenBy(block => block.Id)
                        .ToList());

            NormalizeOrders(rootBlocks);
            foreach (var siblings in childGroups.Values)
            {
                NormalizeOrders(siblings);
            }

            var ordered = new List<NotionBlockSnapshot>(blocks.Count);
            var visited = new HashSet<Guid>();
            foreach (var root in rootBlocks)
            {
                Append(root);
            }

            foreach (var remaining in blocks
                         .Where(block => !visited.Contains(block.Id))
                         .OrderBy(block => block.ParentBlockId)
                         .ThenBy(block => block.Order)
                         .ThenBy(block => block.Id))
            {
                Append(remaining);
            }

            snapshot.Blocks = ordered;

            void AppendChildren(Guid parentId)
            {
                if (!childGroups.TryGetValue(parentId, out var children))
                {
                    return;
                }

                foreach (var child in children)
                {
                    Append(child);
                }
            }

            void Append(NotionBlockSnapshot block)
            {
                if (!visited.Add(block.Id))
                {
                    return;
                }

                ordered.Add(block);
                AppendChildren(block.Id);
            }

            static void NormalizeOrders(IReadOnlyList<NotionBlockSnapshot> siblings)
            {
                for (var order = 0; order < siblings.Count; order++)
                {
                    siblings[order].Order = order;
                }
            }
        }
    }
}
