using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Adds a column to a swimlane node.</summary>
public sealed class AddSwimlaneColumnCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private readonly double _size;

    public AddSwimlaneColumnCommand(DiagramDocument doc, string nodeId, int index, double size)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
        _size = size;
    }

    public string Name => "Add swimlane column";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        if (_index < 0 || _index > data.ColumnCount) return;

        data.ColumnCount++;
        if (_index >= data.ColumnSizes.Count)
            data.ColumnSizes.Add(_size);
        else
            data.ColumnSizes.Insert(_index, _size);

        node.W += _size;

        // Shift children in columns at or after the insertion point
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId && n.SwimlaneColumn >= _index))
        {
            child.SwimlaneColumn++;
        }

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        data.ColumnCount--;
        if (_index < data.ColumnSizes.Count)
            data.ColumnSizes.RemoveAt(_index);

        node.W -= _size;

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId && n.SwimlaneColumn > _index))
        {
            child.SwimlaneColumn--;
        }

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }
}
