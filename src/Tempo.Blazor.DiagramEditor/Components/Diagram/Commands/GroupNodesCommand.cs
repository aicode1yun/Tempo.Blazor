using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Groups the selected nodes under a new group container node (undoable).</summary>
public sealed class GroupNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly DiagramNode _container;
    private readonly IReadOnlyList<string?> _previousParentGroupIds;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public GroupNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _previousParentGroupIds = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)?.ParentGroupId).ToList();
        _previousGroupIds = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)?.GroupId).ToList();

        var nodes = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n is not null).ToList();
        var minX = nodes.Min(n => n!.X);
        var minY = nodes.Min(n => n!.Y);
        var maxX = nodes.Max(n => n!.X + n!.W);
        var maxY = nodes.Max(n => n!.Y + n!.H);
        const double pad = 16;

        _container = new DiagramNode
        {
            StencilId = "general.group",
            X = minX - pad,
            Y = minY - pad,
            W = maxX - minX + pad * 2,
            H = maxY - minY + pad * 2,
            ZIndex = nodes.Min(n => n!.ZIndex) - 1,
            Data = { ["label"] = "Group" }
        };
    }

    public string Name => "Group nodes";

    public void Execute()
    {
        _doc.Nodes.Add(_container);
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.ParentGroupId = _container.Id;
            node.GroupId = _container.Id;
        }
    }

    public void Undo()
    {
        _doc.Nodes.Remove(_container);
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _previousParentGroupIds.Count) continue;
            node.ParentGroupId = _previousParentGroupIds[i];
            if (i < _previousGroupIds.Count) node.GroupId = _previousGroupIds[i];
        }
    }
}
