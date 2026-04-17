using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates swimlane row/column sizes and resizes the container node.</summary>
public sealed class ResizeSwimlaneCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly List<double> _oldRowSizes;
    private readonly List<double> _oldColumnSizes;
    private readonly double _oldW;
    private readonly double _oldH;
    private readonly List<double> _newRowSizes;
    private readonly List<double> _newColumnSizes;
    private readonly double _newW;
    private readonly double _newH;

    public ResizeSwimlaneCommand(
        DiagramDocument doc,
        string nodeId,
        List<double> oldRowSizes,
        List<double> oldColumnSizes,
        double oldW,
        double oldH,
        List<double> newRowSizes,
        List<double> newColumnSizes,
        double newW,
        double newH)
    {
        _doc = doc;
        _nodeId = nodeId;
        _oldRowSizes = new List<double>(oldRowSizes);
        _oldColumnSizes = new List<double>(oldColumnSizes);
        _oldW = oldW;
        _oldH = oldH;
        _newRowSizes = new List<double>(newRowSizes);
        _newColumnSizes = new List<double>(newColumnSizes);
        _newW = newW;
        _newH = newH;
    }

    public string Name => "Resize swimlane";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        data.RowSizes = new List<double>(_newRowSizes);
        data.ColumnSizes = new List<double>(_newColumnSizes);
        node.W = _newW;
        node.H = _newH;

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        data.RowSizes = new List<double>(_oldRowSizes);
        data.ColumnSizes = new List<double>(_oldColumnSizes);
        node.W = _oldW;
        node.H = _oldH;

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }
}
