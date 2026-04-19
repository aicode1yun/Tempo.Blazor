using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Flips the elbow orientation of an edge between horizontal-first and vertical-first (undoable).</summary>
public sealed class FlipEdgeCommand : IDiagramCommand
{
    private readonly DiagramEdge _edge;
    private readonly string _before;
    private readonly string _after;
    private readonly List<DiagramPoint> _beforeWaypoints;
    private readonly List<DiagramPoint> _afterWaypoints;

    public FlipEdgeCommand(DiagramEdge edge, string newOrientation, List<DiagramPoint> newWaypoints)
    {
        _edge = edge;
        _before = edge.ElbowOrientation;
        _after = newOrientation;
        _beforeWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        _afterWaypoints = newWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public string Name => "Flip edge orientation";

    public void Execute()
    {
        _edge.ElbowOrientation = _after;
        _edge.Waypoints.Clear();
        foreach (var wp in _afterWaypoints)
            _edge.Waypoints.Add(wp);
    }

    public void Undo()
    {
        _edge.ElbowOrientation = _before;
        _edge.Waypoints.Clear();
        foreach (var wp in _beforeWaypoints)
            _edge.Waypoints.Add(wp);
    }
}
