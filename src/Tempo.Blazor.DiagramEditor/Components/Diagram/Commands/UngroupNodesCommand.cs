using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Ungroups nodes by removing the group container and reparenting children (undoable).</summary>
public sealed class UngroupNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _groupId;
    private readonly DiagramNode? _container;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly IReadOnlyList<string?> _previousParentGroupIds;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public UngroupNodesCommand(DiagramDocument doc, string groupId)
    {
        _doc = doc;
        _groupId = groupId;
        _container = doc.Nodes.FirstOrDefault(n => n.Id == groupId && n.StencilId == "general.group");
        _nodeIds = doc.Nodes.Where(n => n.ParentGroupId == groupId).Select(n => n.Id).ToList();
        _previousParentGroupIds = _nodeIds.Select(id => doc.Nodes.FirstOrDefault(n => n.Id == id)?.ParentGroupId).ToList();
        _previousGroupIds = _nodeIds.Select(id => doc.Nodes.FirstOrDefault(n => n.Id == id)?.GroupId).ToList();
    }

    public string Name => "Ungroup nodes";

    public void Execute()
    {
        if (_container is not null) _doc.Nodes.Remove(_container);
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.ParentGroupId = null;
            if (node.GroupId == _groupId) node.GroupId = null;
        }
    }

    public void Undo()
    {
        if (_container is not null) _doc.Nodes.Add(_container);
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _previousParentGroupIds.Count) continue;
            node.ParentGroupId = _previousParentGroupIds[i];
            if (i < _previousGroupIds.Count) node.GroupId = _previousGroupIds[i];
        }
    }
}
