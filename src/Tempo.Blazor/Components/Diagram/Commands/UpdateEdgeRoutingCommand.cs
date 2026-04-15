using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the routing and waypoints of an edge (undoable).</summary>
public sealed class UpdateEdgeRoutingCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly string _oldRouting;
    private readonly string _newRouting;
    private readonly List<DiagramPoint> _oldWaypoints;
    private readonly List<DiagramPoint> _newWaypoints;

    public UpdateEdgeRoutingCommand(
        DiagramDocument doc,
        string edgeId,
        string oldRouting,
        string newRouting,
        List<DiagramPoint> oldWaypoints,
        List<DiagramPoint> newWaypoints)
    {
        _doc = doc;
        _edgeId = edgeId;
        _oldRouting = oldRouting;
        _newRouting = newRouting;
        _oldWaypoints = oldWaypoints;
        _newWaypoints = newWaypoints;
    }

    public string Name => "Update edge routing";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Routing = _newRouting;
        edge.Waypoints = _newWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Routing = _oldRouting;
        edge.Waypoints = _oldWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }
}
