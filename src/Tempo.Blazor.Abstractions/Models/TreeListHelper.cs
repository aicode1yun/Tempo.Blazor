namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Helper methods for converting flat data into a hierarchical tree-list representation.</summary>
public static class TreeListHelper
{
    private static readonly object NullParentKey = new();

    /// <summary>
    /// Converts a flat list into a flattened tree-list with hierarchy metadata.
    /// Items are sorted so that children immediately follow their parent.
    /// </summary>
    /// <typeparam name="TItem">The underlying data type.</typeparam>
    /// <param name="items">Flat list of items.</param>
    /// <param name="idSelector">Function that returns the unique id of an item.</param>
    /// <param name="parentIdSelector">Function that returns the parent id, or <c>null</c> for roots.</param>
    /// <param name="expandedIds">Set of ids that should be initially expanded.</param>
    /// <param name="sortBy">Optional function to sort siblings. When null, original order is preserved.</param>
    /// <param name="sortDescending">When true, sorts in descending order.</param>
    /// <returns>Flattened list ordered parent-before-children with hierarchy context.</returns>
    public static IReadOnlyList<TreeListItemContext<TItem>> BuildTree<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, object> idSelector,
        Func<TItem, object?> parentIdSelector,
        IReadOnlySet<object>? expandedIds = null,
        Func<TItem, object>? sortBy = null,
        bool sortDescending = false)
    {
        var list = items.ToList();

        // Build id map (skip null ids as a safety guard)
        var idMap = new Dictionary<object, TItem>(list.Count);
        foreach (var item in list)
        {
            var id = idSelector(item);
            if (id is null) continue;
            idMap[id] = item;
        }

        // Build children map grouped by parent id (null = roots → sentinel key)
        var childrenMap = new Dictionary<object, List<TItem>>(list.Count);
        foreach (var item in list)
        {
            var pid = parentIdSelector(item) ?? NullParentKey;
            if (!childrenMap.TryGetValue(pid, out var children))
            {
                children = new List<TItem>();
                childrenMap[pid] = children;
            }
            children.Add(item);
        }

        // Apply sibling sorting before traversal
        if (sortBy is not null)
        {
            var comparer = Comparer<object>.Default;
            foreach (var kvp in childrenMap.ToList())
            {
                var sorted = sortDescending
                    ? kvp.Value.OrderByDescending(x => sortBy(x), comparer).ToList()
                    : kvp.Value.OrderBy(x => sortBy(x), comparer).ToList();
                childrenMap[kvp.Key] = sorted;
            }
        }

        var result = new List<TreeListItemContext<TItem>>(list.Count);
        var visited = new HashSet<object>(list.Count);

        void Visit(TItem item, int level)
        {
            var id = idSelector(item);
            if (id is null) return;
            if (!visited.Add(id)) return; // guard against cycles

            var pid = parentIdSelector(item);
            bool hasChildren = childrenMap.TryGetValue(id, out var childList) && childList.Count > 0;
            bool isExpanded = expandedIds?.Contains(id) ?? false;
            bool isVisible = level == 0; // roots are always visible; children depend on ancestors

            result.Add(new TreeListItemContext<TItem>
            {
                Item = item,
                Id = id,
                ParentId = pid,
                Level = level,
                HasChildren = hasChildren,
                IsExpanded = isExpanded,
                IsVisible = isVisible
            });

            if (hasChildren)
            {
                foreach (var child in childList!)
                {
                    Visit(child, level + 1);
                }
            }
        }

        // Start with root items (null parent id → sentinel key)
        if (childrenMap.TryGetValue(NullParentKey, out var roots))
        {
            foreach (var root in roots)
            {
                Visit(root, 0);
            }
        }

        // Handle orphaned items (non-null parent id not found in set) as roots at the end
        var orphaned = list.Where(x =>
        {
            var pid = parentIdSelector(x);
            return pid is not null && !idMap.ContainsKey(pid);
        }).ToList();

        foreach (var orphan in orphaned)
        {
            Visit(orphan, 0);
        }

        // Compute IsVisible based on ancestor expansion state
        for (int i = 0; i < result.Count; i++)
        {
            var ctx = result[i];
            if (ctx.Level == 0)
            {
                ctx.IsVisible = true;
                continue;
            }

            // Find parent context
            var parent = result.FirstOrDefault(x => EqualityComparer<object>.Default.Equals(x.Id, ctx.ParentId));
            ctx.IsVisible = parent?.IsVisible == true && parent.IsExpanded;
        }

        return result;
    }
}
