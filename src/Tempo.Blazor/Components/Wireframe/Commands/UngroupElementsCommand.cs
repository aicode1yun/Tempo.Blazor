using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Ungroups elements by removing the group container and reparenting children (undoable).</summary>
public sealed class UngroupElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _groupId;
    private readonly WireframeElement? _container;
    private readonly IReadOnlyList<string> _elementIds;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public UngroupElementsCommand(WireframeDocument doc, string groupId)
    {
        _doc = doc;
        _groupId = groupId;
        _container = doc.Elements.FirstOrDefault(e => e.Id == groupId && e.Type == "__group__");
        _elementIds = doc.Elements.Where(e => e.GroupId == groupId).Select(e => e.Id).ToList();
        _previousGroupIds = _elementIds.Select(id => doc.Elements.FirstOrDefault(e => e.Id == id)?.GroupId).ToList();
    }

    public string Name => "Ungroup";

    public void Execute()
    {
        if (_container is not null) _doc.Elements.Remove(_container);
        foreach (var id in _elementIds)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null) continue;
            if (el.GroupId == _groupId) el.GroupId = null;
        }
    }

    public void Undo()
    {
        if (_container is not null) _doc.Elements.Add(_container);
        for (int i = 0; i < _elementIds.Count; i++)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == _elementIds[i]);
            if (el is null || i >= _previousGroupIds.Count) continue;
            el.GroupId = _previousGroupIds[i];
        }
    }
}
