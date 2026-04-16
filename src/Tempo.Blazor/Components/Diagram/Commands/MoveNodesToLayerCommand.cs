using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Moves selected nodes to a different layer (undoable).</summary>
public sealed class MoveNodesToLayerCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string? _targetLayerId;
    private readonly Dictionary<string, string?> _previousLayerIds;

    public MoveNodesToLayerCommand(DiagramDocument doc, IEnumerable<string> nodeIds, string? targetLayerId)
    {
        _doc = doc;
        _targetLayerId = targetLayerId;
        _previousLayerIds = nodeIds
            .Select(id => doc.Nodes.FirstOrDefault(n => n.Id == id))
            .Where(n => n is not null)
            .ToDictionary(n => n!.Id, n => n.LayerId);
    }

    public string Name => "Move nodes to layer";

    public void Execute()
    {
        foreach (var (nodeId, _) in _previousLayerIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
                node.LayerId = _targetLayerId;
        }
    }

    public void Undo()
    {
        foreach (var (nodeId, prevLayerId) in _previousLayerIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
                node.LayerId = prevLayerId;
        }
    }
}
