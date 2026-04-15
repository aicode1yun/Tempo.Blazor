using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Distributes multiple nodes evenly horizontally or vertically (undoable).</summary>
public sealed class DistributeNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly string _direction; // "horizontal" or "vertical"
    private readonly IReadOnlyList<(double X, double Y)> _beforePositions;
    private readonly IReadOnlyList<(double X, double Y)> _afterPositions;

    public DistributeNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds, string direction)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _direction = direction;
        _beforePositions = _nodeIds.Select(id =>
        {
            var n = doc.Nodes.FirstOrDefault(n => n.Id == id);
            return (n?.X ?? 0, n?.Y ?? 0);
        }).ToList();
        _afterPositions = ComputeDistributedPositions();
    }

    public string Name => $"Distribute {_direction}";

    private IReadOnlyList<(double X, double Y)> ComputeDistributedPositions()
    {
        var nodes = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n is not null).ToList();
        if (nodes.Count < 3) return _beforePositions;

        var result = new List<(double X, double Y)>();

        if (_direction == "horizontal")
        {
            var ordered = nodes.OrderBy(n => n!.X).ToList();
            var minX = ordered.First()!.X;
            var maxX = ordered.Last()!.X;
            var totalWidth = ordered.Sum(n => n!.W);
            var availableSpace = maxX - minX;
            var gap = (availableSpace - totalWidth + ordered.First()!.W + ordered.Last()!.W) / (ordered.Count - 1);
            // Actually simpler: distribute centers or left edges evenly
            // Let's distribute left edges evenly across the span
            var span = maxX - minX;
            var step = span / (ordered.Count - 1);
            for (int i = 0; i < ordered.Count; i++)
            {
                var n = ordered[i]!;
                result.Add((minX + i * step, n.Y));
            }
            // Map back to original node order
            return MapBackToOriginalOrder(ordered, result);
        }
        else // vertical
        {
            var ordered = nodes.OrderBy(n => n!.Y).ToList();
            var minY = ordered.First()!.Y;
            var maxY = ordered.Last()!.Y;
            var span = maxY - minY;
            var step = span / (ordered.Count - 1);
            for (int i = 0; i < ordered.Count; i++)
            {
                var n = ordered[i]!;
                result.Add((n.X, minY + i * step));
            }
            return MapBackToOriginalOrder(ordered, result);
        }
    }

    private IReadOnlyList<(double X, double Y)> MapBackToOriginalOrder(List<DiagramNode?> ordered, List<(double X, double Y)> orderedPositions)
    {
        var map = new Dictionary<string, (double X, double Y)>();
        for (int i = 0; i < ordered.Count; i++)
        {
            map[ordered[i]!.Id] = orderedPositions[i];
        }
        return _nodeIds.Select(id => map.TryGetValue(id, out var pos) ? pos : (0.0, 0.0)).ToList();
    }

    public void Execute()
    {
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _afterPositions.Count) continue;
            node.X = _afterPositions[i].X;
            node.Y = _afterPositions[i].Y;
        }
    }

    public void Undo()
    {
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _beforePositions.Count) continue;
            node.X = _beforePositions[i].X;
            node.Y = _beforePositions[i].Y;
        }
    }
}
