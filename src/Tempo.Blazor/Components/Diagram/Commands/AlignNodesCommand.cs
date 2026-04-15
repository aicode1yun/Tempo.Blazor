using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Aligns multiple nodes horizontally or vertically (undoable).</summary>
public sealed class AlignNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly string _alignment;
    private readonly IReadOnlyList<(double X, double Y)> _beforePositions;
    private readonly IReadOnlyList<(double X, double Y)> _afterPositions;

    public AlignNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds, string alignment)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _alignment = alignment;
        _beforePositions = _nodeIds.Select(id =>
        {
            var n = doc.Nodes.FirstOrDefault(n => n.Id == id);
            return (n?.X ?? 0, n?.Y ?? 0);
        }).ToList();
        _afterPositions = ComputeAlignedPositions();
    }

    public string Name => $"Align {_alignment}";

    private IReadOnlyList<(double X, double Y)> ComputeAlignedPositions()
    {
        var nodes = _nodeIds.Select(id => _doc.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n is not null).ToList();
        if (nodes.Count == 0) return [];

        var result = new List<(double X, double Y)>();
        switch (_alignment)
        {
            case "left":
                var minX = nodes.Min(n => n!.X);
                foreach (var n in nodes) result.Add((minX, n!.Y));
                break;
            case "center":
                var avgCx = nodes.Average(n => n!.X + n.W / 2);
                foreach (var n in nodes) result.Add((avgCx - n!.W / 2, n.Y));
                break;
            case "right":
                var maxRight = nodes.Max(n => n!.X + n!.W);
                foreach (var n in nodes) result.Add((maxRight - n!.W, n.Y));
                break;
            case "top":
                var minY = nodes.Min(n => n!.Y);
                foreach (var n in nodes) result.Add((n!.X, minY));
                break;
            case "middle":
                var avgCy = nodes.Average(n => n!.Y + n.H / 2);
                foreach (var n in nodes) result.Add((n!.X, avgCy - n.H / 2));
                break;
            case "bottom":
                var maxBottom = nodes.Max(n => n!.Y + n!.H);
                foreach (var n in nodes) result.Add((n!.X, maxBottom - n!.H));
                break;
            default:
                foreach (var n in nodes) result.Add((n!.X, n.Y));
                break;
        }
        return result;
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
