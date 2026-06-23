using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Removes a column from a swimlane node.</summary>
public sealed class RemoveSwimlaneColumnCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private double _removedSize;
    private readonly List<(string ChildId, int OldRow, int OldCol)> _childStates = [];

    public RemoveSwimlaneColumnCommand(DiagramDocument doc, string nodeId, int index)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
    }

    public string Name => "Remove swimlane column";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;
        if (_index < 0 || _index >= data.ColumnCount) return;

        _removedSize = _index < data.ColumnSizes.Count ? data.ColumnSizes[_index] : 0;
        _childStates.Clear();

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            _childStates.Add((child.Id, child.SwimlaneRow, child.SwimlaneColumn));
        }

        data.ColumnCount--;
        if (_index < data.ColumnSizes.Count)
            data.ColumnSizes.RemoveAt(_index);

        node.W -= _removedSize;

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId).ToList())
        {
            if (child.SwimlaneColumn == _index)
            {
                child.ParentNodeId = null;
                child.SwimlaneRow = -1;
                child.SwimlaneColumn = -1;
            }
            else if (child.SwimlaneColumn > _index)
            {
                child.SwimlaneColumn--;
            }
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

        data.ColumnCount++;
        if (_index >= data.ColumnSizes.Count)
            data.ColumnSizes.Add(_removedSize);
        else
            data.ColumnSizes.Insert(_index, _removedSize);

        node.W += _removedSize;

        foreach (var state in _childStates)
        {
            var child = _doc.Nodes.FirstOrDefault(n => n.Id == state.ChildId);
            if (child is null) continue;
            child.ParentNodeId = _nodeId;
            child.SwimlaneRow = state.OldRow;
            child.SwimlaneColumn = state.OldCol;
        }

        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            SwimlaneLayoutService.ArrangeChild(node, child);
        }
    }
}
