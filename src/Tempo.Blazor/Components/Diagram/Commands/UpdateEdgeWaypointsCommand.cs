using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the waypoints of a single edge.</summary>
public sealed class UpdateEdgeWaypointsCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly List<DiagramPoint> _oldWaypoints;
    private readonly List<DiagramPoint> _newWaypoints;

    public UpdateEdgeWaypointsCommand(
        DiagramDocument doc,
        string edgeId,
        List<DiagramPoint> oldWaypoints,
        List<DiagramPoint> newWaypoints)
    {
        _doc = doc;
        _edgeId = edgeId;
        _oldWaypoints = oldWaypoints;
        _newWaypoints = newWaypoints;
    }

    public string Name => "Update edge path";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Waypoints = _newWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Waypoints = _oldWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }
}
