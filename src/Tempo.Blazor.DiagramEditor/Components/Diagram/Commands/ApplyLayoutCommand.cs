using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Applies a computed layout to diagram nodes (undoable).</summary>
public sealed class ApplyLayoutCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly Dictionary<string, (double X, double Y)> _previousPositions;
    private readonly Dictionary<string, (double X, double Y)> _newPositions;

    public ApplyLayoutCommand(DiagramDocument doc, Dictionary<string, (double X, double Y)> newPositions)
    {
        _doc = doc;
        _newPositions = newPositions;
        _previousPositions = _newPositions.Keys.ToDictionary(
            id => id,
            id =>
            {
                var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
                return node is not null ? (node.X, node.Y) : (0.0, 0.0);
            });
    }

    public string Name => "Apply layout";

    public void Execute()
    {
        foreach (var (id, pos) in _newPositions)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.X = pos.X;
            node.Y = pos.Y;
        }
    }

    public void Undo()
    {
        foreach (var (id, pos) in _previousPositions)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.X = pos.X;
            node.Y = pos.Y;
        }
    }
}
