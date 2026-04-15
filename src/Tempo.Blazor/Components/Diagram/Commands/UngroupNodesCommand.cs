using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Ungroups nodes by clearing their group identifier (undoable).</summary>
public sealed class UngroupNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _groupId;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public UngroupNodesCommand(DiagramDocument doc, string groupId)
    {
        _doc = doc;
        _groupId = groupId;
        _nodeIds = doc.Nodes.Where(n => n.GroupId == groupId).Select(n => n.Id).ToList();
        _previousGroupIds = _nodeIds.Select(id => doc.Nodes.FirstOrDefault(n => n.Id == id)?.GroupId).ToList();
    }

    public string Name => "Ungroup nodes";

    public void Execute()
    {
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.GroupId = null;
        }
    }

    public void Undo()
    {
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _previousGroupIds.Count) continue;
            node.GroupId = _previousGroupIds[i];
        }
    }
}
