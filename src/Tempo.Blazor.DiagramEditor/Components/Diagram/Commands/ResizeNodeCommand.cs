using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Resizes a single node.</summary>
public sealed class ResizeNodeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly double _oldW;
    private readonly double _oldH;
    private readonly double _newX;
    private readonly double _newY;
    private readonly double _newW;
    private readonly double _newH;

    public ResizeNodeCommand(
        DiagramDocument doc,
        string nodeId,
        double oldX, double oldY, double oldW, double oldH,
        double newX, double newY, double newW, double newH)
    {
        _doc = doc;
        _nodeId = nodeId;
        _oldX = oldX; _oldY = oldY; _oldW = oldW; _oldH = oldH;
        _newX = newX; _newY = newY; _newW = newW; _newH = newH;
    }

    public string Name => "Resize node";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        node.X = _newX; node.Y = _newY; node.W = _newW; node.H = _newH;
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        node.X = _oldX; node.Y = _oldY; node.W = _oldW; node.H = _oldH;
    }
}
