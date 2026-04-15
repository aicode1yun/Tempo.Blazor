using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Deletes a waypoint from an edge path (undoable).</summary>
public sealed class DeleteEdgeWaypointCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly int _index;
    private readonly DiagramPoint _removedPoint;

    public DeleteEdgeWaypointCommand(DiagramDocument doc, string edgeId, int index, DiagramPoint removedPoint)
    {
        _doc = doc;
        _edgeId = edgeId;
        _index = index;
        _removedPoint = removedPoint;
    }

    public string Name => "Delete edge waypoint";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        if (_index >= 0 && _index < edge.Waypoints.Count)
            edge.Waypoints.RemoveAt(_index);
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        if (_index < 0 || _index > edge.Waypoints.Count)
            edge.Waypoints.Add(new DiagramPoint(_removedPoint.X, _removedPoint.Y));
        else
            edge.Waypoints.Insert(_index, new DiagramPoint(_removedPoint.X, _removedPoint.Y));
    }
}
