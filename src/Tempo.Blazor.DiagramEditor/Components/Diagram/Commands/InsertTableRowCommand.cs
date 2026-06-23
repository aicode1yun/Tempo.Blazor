using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Inserts a row into a table node.</summary>
public sealed class InsertTableRowCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly int _index;
    private readonly double _rowHeight;

    public InsertTableRowCommand(DiagramDocument doc, string nodeId, int index, double rowHeight = 30)
    {
        _doc = doc;
        _nodeId = nodeId;
        _index = index;
        _rowHeight = rowHeight;
    }

    public string Name => "Insert table row";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        TableLayoutService.InsertRow(node, _index);
        node.H += _rowHeight;
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        TableLayoutService.DeleteRow(node, _index);
        node.H -= _rowHeight;
    }
}
