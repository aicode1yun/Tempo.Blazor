using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Removes a layer and moves its nodes to the default (null) layer (undoable).</summary>
public sealed class RemoveLayerCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly DiagramLayer _layer;
    private readonly int _originalIndex;
    private readonly List<(string NodeId, string? PreviousLayerId)> _movedNodes = [];

    public RemoveLayerCommand(DiagramDocument doc, string layerId)
    {
        _doc = doc;
        _layer = doc.Layers.First(l => l.Id == layerId);
        _originalIndex = doc.Layers.IndexOf(_layer);
        _movedNodes = doc.Nodes
            .Where(n => n.LayerId == layerId)
            .Select(n => (n.Id, n.LayerId))
            .ToList();
    }

    public string Name => "Remove layer";

    public void Execute()
    {
        _doc.Layers.Remove(_layer);
        var defaultLayerId = _doc.Layers.OrderBy(l => l.Order).FirstOrDefault()?.Id;
        foreach (var node in _doc.Nodes.Where(n => n.LayerId == _layer.Id))
            node.LayerId = defaultLayerId;
    }

    public void Undo()
    {
        if (_originalIndex >= 0 && _originalIndex <= _doc.Layers.Count)
            _doc.Layers.Insert(_originalIndex, _layer);
        else
            _doc.Layers.Add(_layer);

        foreach (var (nodeId, prevLayerId) in _movedNodes)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
                node.LayerId = prevLayerId;
        }
    }
}
