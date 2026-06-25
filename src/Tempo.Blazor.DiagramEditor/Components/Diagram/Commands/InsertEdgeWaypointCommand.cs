using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Inserts a waypoint into an edge path (undoable).</summary>
public sealed class InsertEdgeWaypointCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly int _index;
    private readonly DiagramPoint _point;

    public InsertEdgeWaypointCommand(DiagramDocument doc, string edgeId, int index, DiagramPoint point)
    {
        _doc = doc;
        _edgeId = edgeId;
        _index = index;
        _point = point;
    }

    public string Name => "Insert edge waypoint";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.IsManuallyRouted = true;
        if (_index < 0 || _index > edge.Waypoints.Count)
            edge.Waypoints.Add(new DiagramPoint(_point.X, _point.Y));
        else
            edge.Waypoints.Insert(_index, new DiagramPoint(_point.X, _point.Y));
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        if (_index >= 0 && _index < edge.Waypoints.Count)
            edge.Waypoints.RemoveAt(_index);
        else if (edge.Waypoints.Count > 0)
            edge.Waypoints.RemoveAt(edge.Waypoints.Count - 1);
    }
}
