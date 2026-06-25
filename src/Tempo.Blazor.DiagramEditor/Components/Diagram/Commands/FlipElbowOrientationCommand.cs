using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Toggles the elbow orientation of an edge between horizontal-first and vertical-first (undoable).</summary>
public sealed class FlipElbowOrientationCommand : IDiagramCommand
{
    private readonly DiagramEdge _edge;
    private readonly string _beforeOrientation;
    private readonly string _afterOrientation;
    private readonly List<DiagramPoint> _beforeWaypoints;
    private readonly List<DiagramPoint> _afterWaypoints;

    public FlipElbowOrientationCommand(DiagramEdge edge, string newOrientation, List<DiagramPoint> newWaypoints)
    {
        _edge = edge;
        _beforeOrientation = edge.ElbowOrientation;
        _afterOrientation = newOrientation;
        _beforeWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        _afterWaypoints = newWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public string Name => "Flip elbow orientation";

    public void Execute()
    {
        _edge.ElbowOrientation = _afterOrientation;
        _edge.Waypoints.Clear();
        foreach (var wp in _afterWaypoints)
            _edge.Waypoints.Add(wp);
    }

    public void Undo()
    {
        _edge.ElbowOrientation = _beforeOrientation;
        _edge.Waypoints.Clear();
        foreach (var wp in _beforeWaypoints)
            _edge.Waypoints.Add(wp);
    }
}
