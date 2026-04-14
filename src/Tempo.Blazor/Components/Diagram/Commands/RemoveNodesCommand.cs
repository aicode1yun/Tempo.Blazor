using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Removes one or more nodes from the diagram document.</summary>
public sealed class RemoveNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<DiagramNode> _removedNodes;
    private readonly IReadOnlyList<DiagramEdge> _removedEdges;

    public RemoveNodesCommand(DiagramDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        var idSet = ids.ToHashSet();

        _removedNodes = doc.Nodes.Where(n => idSet.Contains(n.Id)).ToList();

        // Also remove any edges connected to removed nodes
        var connectedEdgeIds = doc.Edges
            .Where(e => idSet.Contains(e.SourceNodeId) || idSet.Contains(e.TargetNodeId))
            .Select(e => e.Id)
            .ToHashSet();
        _removedEdges = doc.Edges.Where(e => connectedEdgeIds.Contains(e.Id)).ToList();
    }

    public string Name => _removedNodes.Count == 1
        ? $"Delete {_removedNodes[0].StencilId}"
        : $"Delete {_removedNodes.Count} nodes";

    public void Execute()
    {
        var nodeIdSet = _removedNodes.Select(n => n.Id).ToHashSet();
        var edgeIdSet = _removedEdges.Select(e => e.Id).ToHashSet();

        _doc.Nodes.RemoveAll(n => nodeIdSet.Contains(n.Id));
        _doc.Edges.RemoveAll(e => edgeIdSet.Contains(e.Id));
    }

    public void Undo()
    {
        foreach (var node in _removedNodes)
        {
            if (!_doc.Nodes.Any(n => n.Id == node.Id))
                _doc.Nodes.Add(node);
        }
        foreach (var edge in _removedEdges)
        {
            if (!_doc.Edges.Any(e => e.Id == edge.Id))
                _doc.Edges.Add(edge);
        }
        _doc.Nodes.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
    }
}
