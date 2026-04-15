using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Groups the selected nodes under a new group identifier (undoable).</summary>
public sealed class GroupNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly string _groupId;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public GroupNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _groupId = Guid.NewGuid().ToString("N")[..8];
        _previousGroupIds = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)?.GroupId).ToList();
    }

    public string Name => "Group nodes";

    public void Execute()
    {
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.GroupId = _groupId;
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
