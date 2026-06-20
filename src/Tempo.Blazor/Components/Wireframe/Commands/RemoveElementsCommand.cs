using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Removes one or more elements from the document (Delete key / toolbar).</summary>
public sealed class RemoveElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<WireframeElement> _removed;

    /// <param name="doc">Target document.</param>
    /// <param name="ids">Ids of the elements to remove.</param>
    public RemoveElementsCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        var idSet = ids.ToHashSet();
        // Snapshot only non-locked elements before removal so Undo can restore them
        var removed = doc.Elements.Where(e => idSet.Contains(e.Id) && !e.IsLocked && string.IsNullOrEmpty(e.LockedBy)).ToList();

        // Cascade: if a group is being deleted, also delete its children
        var groupIds = removed.Where(e => e.Type == "__group__").Select(e => e.Id).ToHashSet();
        if (groupIds.Count > 0)
        {
            var childIds = doc.Elements
                .Where(e => e.GroupId != null && groupIds.Contains(e.GroupId) && !e.IsLocked && string.IsNullOrEmpty(e.LockedBy))
                .Select(e => e.Id)
                .ToHashSet();
            var allIds = ids.ToHashSet();
            allIds.UnionWith(childIds);
            removed = doc.Elements.Where(e => allIds.Contains(e.Id) && !e.IsLocked && string.IsNullOrEmpty(e.LockedBy)).ToList();
        }

        _removed = removed;
    }

    public string Name => _removed.Count == 1
        ? $"Delete {_removed[0].Type}"
        : $"Delete {_removed.Count} elements";

    public void Execute()
    {
        var idSet = _removed.Select(e => e.Id).ToHashSet();
        _doc.Elements.RemoveAll(e => idSet.Contains(e.Id));
    }

    public void Undo()
    {
        foreach (var el in _removed)
        {
            if (!_doc.Elements.Any(e => e.Id == el.Id))
                _doc.Elements.Add(el);
        }
        // Restore original order by ZIndex
        _doc.Elements.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
    }
}
