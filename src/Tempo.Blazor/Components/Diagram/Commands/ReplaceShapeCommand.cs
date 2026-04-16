using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Replaces the stencil of a single node, regenerating ports and remapping connected edges.</summary>
public sealed class ReplaceShapeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly string _newStencilId;
    private readonly List<DiagramPort> _newPorts;
    private readonly double _newW;
    private readonly double _newH;

    private string _oldStencilId = string.Empty;
    private List<DiagramPort> _oldPorts = [];
    private double _oldW;
    private double _oldH;
    private readonly Dictionary<string, string?> _oldSourcePortIds = [];
    private readonly Dictionary<string, string?> _oldTargetPortIds = [];

    public ReplaceShapeCommand(DiagramDocument doc, string nodeId, string newStencilId, List<DiagramPort> newPorts, double newW, double newH)
    {
        _doc = doc;
        _nodeId = nodeId;
        _newStencilId = newStencilId;
        _newPorts = newPorts;
        _newW = newW;
        _newH = newH;
    }

    public string Name => "Replace shape";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;

        // Snapshot old state
        _oldStencilId = node.StencilId;
        _oldPorts = DeepCopyPorts(node.Ports);
        _oldW = node.W;
        _oldH = node.H;

        _oldSourcePortIds.Clear();
        _oldTargetPortIds.Clear();

        foreach (var edge in _doc.Edges.Where(e => e.SourceNodeId == _nodeId || e.TargetNodeId == _nodeId))
        {
            if (edge.SourceNodeId == _nodeId && !_oldSourcePortIds.ContainsKey(edge.Id))
                _oldSourcePortIds[edge.Id] = edge.SourcePortId;
            if (edge.TargetNodeId == _nodeId && !_oldTargetPortIds.ContainsKey(edge.Id))
                _oldTargetPortIds[edge.Id] = edge.TargetPortId;
        }

        // Apply new shape
        node.StencilId = _newStencilId;
        node.Ports = DeepCopyPorts(_newPorts);
        node.W = _newW;
        node.H = _newH;

        // Remap connected edges
        foreach (var edge in _doc.Edges.Where(e => e.SourceNodeId == _nodeId || e.TargetNodeId == _nodeId))
        {
            if (edge.SourceNodeId == _nodeId)
            {
                var oldSide = _oldSourcePortIds.TryGetValue(edge.Id, out var oldPid)
                    ? _oldPorts.FirstOrDefault(p => p.Id == oldPid)?.Side
                    : null;
                edge.SourcePortId = FindBestPort(node.Ports, oldSide, isOutput: true);
            }
            if (edge.TargetNodeId == _nodeId)
            {
                var oldSide = _oldTargetPortIds.TryGetValue(edge.Id, out var oldPid)
                    ? _oldPorts.FirstOrDefault(p => p.Id == oldPid)?.Side
                    : null;
                edge.TargetPortId = FindBestPort(node.Ports, oldSide, isInput: true);
            }
        }
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;

        node.StencilId = _oldStencilId;
        node.Ports = DeepCopyPorts(_oldPorts);
        node.W = _oldW;
        node.H = _oldH;

        foreach (var edge in _doc.Edges.Where(e => e.SourceNodeId == _nodeId || e.TargetNodeId == _nodeId))
        {
            if (edge.SourceNodeId == _nodeId && _oldSourcePortIds.TryGetValue(edge.Id, out var oldSource))
                edge.SourcePortId = oldSource;
            if (edge.TargetNodeId == _nodeId && _oldTargetPortIds.TryGetValue(edge.Id, out var oldTarget))
                edge.TargetPortId = oldTarget;
        }
    }

    private static string? FindBestPort(List<DiagramPort> ports, PortSide? preferredSide, bool isInput = false, bool isOutput = false)
    {
        if (ports.Count == 0) return null;

        IEnumerable<DiagramPort> candidates = ports;
        if (isInput && !isOutput)
            candidates = ports.Where(p => p.IsInput);
        else if (isOutput && !isInput)
            candidates = ports.Where(p => p.IsOutput);

        var list = candidates.ToList();
        if (list.Count == 0)
            list = ports;

        if (preferredSide.HasValue)
        {
            var sameSide = list.FirstOrDefault(p => p.Side == preferredSide.Value);
            if (sameSide is not null)
                return sameSide.Id;
        }

        return list.First().Id;
    }

    private static List<DiagramPort> DeepCopyPorts(List<DiagramPort> ports)
    {
        var json = JsonSerializer.Serialize(ports);
        return JsonSerializer.Deserialize<List<DiagramPort>>(json) ?? [];
    }
}
