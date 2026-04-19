using System.Collections.Generic;
using System.Linq;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Drags an entire edge as a unit, detaching both ends from nodes/ports/constraints/edges and shifting all points by delta.</summary>
public sealed class DragWholeEdgeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly double _dx;
    private readonly double _dy;

    // Snapshots for undo
    private readonly string? _oldSourceNodeId;
    private readonly string? _oldTargetNodeId;
    private readonly string? _oldSourcePortId;
    private readonly string? _oldTargetPortId;
    private readonly DiagramConnectionConstraint? _oldSourceConstraint;
    private readonly DiagramConnectionConstraint? _oldTargetConstraint;
    private readonly string? _oldSourceEdgeId;
    private readonly string? _oldTargetEdgeId;
    private readonly double? _oldSourceEdgeT;
    private readonly double? _oldTargetEdgeT;
    private readonly bool _oldIsManuallyRouted;
    private readonly DiagramPoint? _oldSourcePoint;
    private readonly DiagramPoint? _oldTargetPoint;
    private readonly List<DiagramPoint> _oldWaypoints;

    public DragWholeEdgeCommand(DiagramDocument doc, string edgeId, double dx, double dy)
    {
        _doc = doc;
        _edgeId = edgeId;
        _dx = dx;
        _dy = dy;

        var edge = _doc.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null)
        {
            _oldWaypoints = [];
            return;
        }

        _oldSourceNodeId = edge.SourceNodeId;
        _oldTargetNodeId = edge.TargetNodeId;
        _oldSourcePortId = edge.SourcePortId;
        _oldTargetPortId = edge.TargetPortId;
        _oldSourceConstraint = edge.SourceConstraint is not null ? new DiagramConnectionConstraint
        {
            RelativeX = edge.SourceConstraint.RelativeX,
            RelativeY = edge.SourceConstraint.RelativeY,
            Perimeter = edge.SourceConstraint.Perimeter,
            Dx = edge.SourceConstraint.Dx,
            Dy = edge.SourceConstraint.Dy
        } : null;
        _oldTargetConstraint = edge.TargetConstraint is not null ? new DiagramConnectionConstraint
        {
            RelativeX = edge.TargetConstraint.RelativeX,
            RelativeY = edge.TargetConstraint.RelativeY,
            Perimeter = edge.TargetConstraint.Perimeter,
            Dx = edge.TargetConstraint.Dx,
            Dy = edge.TargetConstraint.Dy
        } : null;
        _oldSourceEdgeId = edge.SourceEdgeId;
        _oldTargetEdgeId = edge.TargetEdgeId;
        _oldSourceEdgeT = edge.SourceEdgeT;
        _oldTargetEdgeT = edge.TargetEdgeT;
        _oldIsManuallyRouted = edge.IsManuallyRouted;
        _oldSourcePoint = edge.SourcePoint is not null ? new DiagramPoint(edge.SourcePoint.X, edge.SourcePoint.Y) : null;
        _oldTargetPoint = edge.TargetPoint is not null ? new DiagramPoint(edge.TargetPoint.X, edge.TargetPoint.Y) : null;
        _oldWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public string Name => "Drag edge";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        // Compute current endpoint positions if not already set as dangling points
        var points = DiagramGeometryHelper.GetEdgePoints(_doc, edge);
        if (edge.SourcePoint is null && points.Length > 0)
        {
            edge.SourcePoint = new DiagramPoint(points[0].X, points[0].Y);
        }
        if (edge.TargetPoint is null && points.Length > 1)
        {
            edge.TargetPoint = new DiagramPoint(points[^1].X, points[^1].Y);
        }

        // Detach both ends
        edge.SourceNodeId = null;
        edge.TargetNodeId = null;
        edge.SourcePortId = null;
        edge.TargetPortId = null;
        edge.SourceConstraint = null;
        edge.TargetConstraint = null;
        edge.SourceEdgeId = null;
        edge.TargetEdgeId = null;
        edge.SourceEdgeT = null;
        edge.TargetEdgeT = null;
        edge.IsManuallyRouted = true;

        // Shift endpoints
        if (edge.SourcePoint is not null)
        {
            edge.SourcePoint.X += _dx;
            edge.SourcePoint.Y += _dy;
        }
        if (edge.TargetPoint is not null)
        {
            edge.TargetPoint.X += _dx;
            edge.TargetPoint.Y += _dy;
        }

        // Shift waypoints
        foreach (var wp in edge.Waypoints)
        {
            wp.X += _dx;
            wp.Y += _dy;
        }
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        edge.SourceNodeId = _oldSourceNodeId;
        edge.TargetNodeId = _oldTargetNodeId;
        edge.SourcePortId = _oldSourcePortId;
        edge.TargetPortId = _oldTargetPortId;
        edge.SourceConstraint = _oldSourceConstraint;
        edge.TargetConstraint = _oldTargetConstraint;
        edge.SourceEdgeId = _oldSourceEdgeId;
        edge.TargetEdgeId = _oldTargetEdgeId;
        edge.SourceEdgeT = _oldSourceEdgeT;
        edge.TargetEdgeT = _oldTargetEdgeT;
        edge.IsManuallyRouted = _oldIsManuallyRouted;
        edge.SourcePoint = _oldSourcePoint is not null ? new DiagramPoint(_oldSourcePoint.X, _oldSourcePoint.Y) : null;
        edge.TargetPoint = _oldTargetPoint is not null ? new DiagramPoint(_oldTargetPoint.X, _oldTargetPoint.Y) : null;
        edge.Waypoints = _oldWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }
}
