using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Adds a row to a swimlane node.</summary>
public sealed class AddSwimlaneRowCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private readonly double _size;

    public AddSwimlaneRowCommand(DiagramDocument doc, string nodeId, int index, double size)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
        _size = size;
    }

    public string Name => "Add swimlane row";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        if (_index < 0 || _index > data.RowCount) return;

        data.RowCount++;
        if (_index >= data.RowSizes.Count)
            data.RowSizes.Add(_size);
        else
            data.RowSizes.Insert(_index, _size);

        node.H += _size;

        // Shift cell labels
        while (data.CellLabels.Count < data.RowCount * data.ColumnCount)
            data.CellLabels.Add($"Lane {data.CellLabels.Count + 1}");

        // Shift children in rows at or after the insertion point
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId && n.SwimlaneRow >= _index))
        {
            child.SwimlaneRow++;
        }

        // Arrange children inside the swimlane
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;

        data.RowCount--;
        if (_index < data.RowSizes.Count)
            data.RowSizes.RemoveAt(_index);

        node.H -= _size;

        // Shift children back
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId && n.SwimlaneRow > _index))
        {
            child.SwimlaneRow--;
        }

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }
}
