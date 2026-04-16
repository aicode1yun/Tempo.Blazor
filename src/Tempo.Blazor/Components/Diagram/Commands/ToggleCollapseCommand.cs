using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Undoable command that toggles the collapsed state of a node.</summary>
public sealed class ToggleCollapseCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly bool _wasCollapsed;
    private readonly double _previousHeight;
    private readonly double _previousExpandedHeight;

    public ToggleCollapseCommand(DiagramDocument doc, string nodeId)
    {
        _doc = doc;
        _nodeId = nodeId;
        var node = doc.Nodes.First(n => n.Id == nodeId);
        _wasCollapsed = node.Collapsed;
        _previousHeight = node.H;
        _previousExpandedHeight = node.ExpandedHeight;
    }

    public string Name => _wasCollapsed ? "Expand node" : "Collapse node";

    public void Execute()
    {
        var node = _doc.Nodes.First(n => n.Id == _nodeId);
        if (!node.IsCollapsible) return;

        if (_wasCollapsed)
        {
            // Expand
            node.Collapsed = false;
            if (node.ExpandedHeight > 0)
                node.H = node.ExpandedHeight;
        }
        else
        {
            // Collapse
            node.ExpandedHeight = node.H;
            node.Collapsed = true;
            node.H = 40;
        }
    }

    public void Undo()
    {
        var node = _doc.Nodes.First(n => n.Id == _nodeId);
        node.Collapsed = _wasCollapsed;
        node.H = _previousHeight;
        node.ExpandedHeight = _previousExpandedHeight;
    }
}
