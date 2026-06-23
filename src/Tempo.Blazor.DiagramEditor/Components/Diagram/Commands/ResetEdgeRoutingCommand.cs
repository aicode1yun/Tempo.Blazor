using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Resets an edge from manual routing back to automatic routing (undoable).</summary>
public sealed class ResetEdgeRoutingCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly bool _oldIsManuallyRouted;
    private readonly List<DiagramPoint> _oldWaypoints;

    public ResetEdgeRoutingCommand(DiagramDocument doc, string edgeId)
    {
        _doc = doc;
        _edgeId = edgeId;

        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is not null)
        {
            _oldIsManuallyRouted = edge.IsManuallyRouted;
            _oldWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        }
        else
        {
            _oldWaypoints = [];
        }
    }

    public string Name => "Reset edge routing";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.IsManuallyRouted = false;
        edge.Waypoints.Clear();
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.IsManuallyRouted = _oldIsManuallyRouted;
        edge.Waypoints = _oldWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }
}
