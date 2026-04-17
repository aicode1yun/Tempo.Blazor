using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Removes a row from a swimlane node.</summary>
public sealed class RemoveSwimlaneRowCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private double _removedSize;
    private readonly List<(string ChildId, int OldRow, int OldCol)> _childStates = [];

    public RemoveSwimlaneRowCommand(DiagramDocument doc, string nodeId, int index)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
    }

    public string Name => "Remove swimlane row";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node?.SwimlaneData is not { } data) return;
        if (_index < 0 || _index >= data.RowCount) return;

        _removedSize = _index < data.RowSizes.Count ? data.RowSizes[_index] : 0;
        _childStates.Clear();

        // Save child states
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId))
        {
            _childStates.Add((child.Id, child.SwimlaneRow, child.SwimlaneColumn));
        }

        data.RowCount--;
        if (_index < data.RowSizes.Count)
            data.RowSizes.RemoveAt(_index);

        node.H -= _removedSize;

        // Detach children in the removed row; shift others up
        foreach (var child in _doc.Nodes.Where(n => n.ParentNodeId == _nodeId).ToList())
        {
            if (child.SwimlaneRow == _index)
            {
                child.ParentNodeId = null;
                child.SwimlaneRow = -1;
                child.SwimlaneColumn = -1;
            }
            else if (child.SwimlaneRow > _index)
            {
                child.SwimlaneRow--;
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

        data.RowCount++;
        if (_index >= data.RowSizes.Count)
            data.RowSizes.Add(_removedSize);
        else
            data.RowSizes.Insert(_index, _removedSize);

        node.H += _removedSize;

        // Restore children
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
