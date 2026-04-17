using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Inserts a column into a table node.</summary>
public sealed class InsertTableColumnCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private readonly double _columnWidth;

    public InsertTableColumnCommand(DiagramDocument doc, string nodeId, int index, double columnWidth = 60)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
        _columnWidth = columnWidth;
    }

    public string Name => "Insert table column";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        TableLayoutService.InsertColumn(node, _index);
        node.W += _columnWidth;
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        TableLayoutService.DeleteColumn(node, _index);
        node.W -= _columnWidth;
    }
}
