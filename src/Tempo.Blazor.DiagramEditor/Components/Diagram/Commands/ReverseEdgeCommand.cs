using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Reverses the direction of an edge by swapping source/target endpoints and arrowheads (undoable).</summary>
public sealed class ReverseEdgeCommand : IDiagramCommand
{
    private readonly DiagramEdge _edge;

    private readonly string? _beforeSourceNodeId;
    private readonly string? _beforeTargetNodeId;
    private readonly string? _beforeSourcePortId;
    private readonly string? _beforeTargetPortId;
    private readonly string? _beforeSourceEdgeId;
    private readonly string? _beforeTargetEdgeId;
    private readonly double? _beforeSourceEdgeT;
    private readonly double? _beforeTargetEdgeT;
    private readonly DiagramPoint? _beforeSourcePoint;
    private readonly DiagramPoint? _beforeTargetPoint;
    private readonly DiagramConnectionConstraint? _beforeSourceConstraint;
    private readonly DiagramConnectionConstraint? _beforeTargetConstraint;
    private readonly double? _beforeSourceSpacing;
    private readonly double? _beforeTargetSpacing;
    private readonly string? _beforeSourceCardinality;
    private readonly string? _beforeTargetCardinality;
    private readonly string _beforeStartArrow;
    private readonly string _beforeEndArrow;
    private readonly double? _beforeStartArrowSize;
    private readonly double? _beforeEndArrowSize;
    private readonly bool? _beforeStartArrowFill;
    private readonly bool? _beforeEndArrowFill;
    private readonly double _beforeLabelPositionT;
    private readonly List<DiagramPoint> _beforeWaypoints;
    private readonly List<DiagramPoint> _afterWaypoints;

    public ReverseEdgeCommand(DiagramEdge edge, List<DiagramPoint>? afterWaypoints = null)
    {
        _edge = edge;

        _beforeSourceNodeId = edge.SourceNodeId;
        _beforeTargetNodeId = edge.TargetNodeId;
        _beforeSourcePortId = edge.SourcePortId;
        _beforeTargetPortId = edge.TargetPortId;
        _beforeSourceEdgeId = edge.SourceEdgeId;
        _beforeTargetEdgeId = edge.TargetEdgeId;
        _beforeSourceEdgeT = edge.SourceEdgeT;
        _beforeTargetEdgeT = edge.TargetEdgeT;
        _beforeSourcePoint = edge.SourcePoint is null ? null : new DiagramPoint(edge.SourcePoint.X, edge.SourcePoint.Y);
        _beforeTargetPoint = edge.TargetPoint is null ? null : new DiagramPoint(edge.TargetPoint.X, edge.TargetPoint.Y);
        _beforeSourceConstraint = edge.SourceConstraint?.Clone();
        _beforeTargetConstraint = edge.TargetConstraint?.Clone();
        _beforeSourceSpacing = edge.SourceSpacing;
        _beforeTargetSpacing = edge.TargetSpacing;
        _beforeSourceCardinality = edge.SourceCardinality;
        _beforeTargetCardinality = edge.TargetCardinality;
        // NOTE: StartArrow/EndArrow are NOT swapped because they describe the
        // physical start/end of the edge line. When source and target are swapped,
        // the physical start and end are already reversed, so the arrowheads
        // automatically move to the opposite node without needing to swap.
        _beforeStartArrow = edge.StartArrow;
        _beforeEndArrow = edge.EndArrow;
        _beforeStartArrowSize = edge.StartArrowSize;
        _beforeEndArrowSize = edge.EndArrowSize;
        _beforeStartArrowFill = edge.StartArrowFill;
        _beforeEndArrowFill = edge.EndArrowFill;
        _beforeLabelPositionT = edge.LabelPositionT;
        _beforeWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();

        _afterWaypoints = afterWaypoints?.Select(p => new DiagramPoint(p.X, p.Y)).ToList()
            ?? _beforeWaypoints.AsEnumerable().Reverse().Select(p => new DiagramPoint(p.X, p.Y)).ToList();
    }

    public string Name => "Reverse edge";

    public void Execute()
    {
        _edge.SourceNodeId = _beforeTargetNodeId;
        _edge.TargetNodeId = _beforeSourceNodeId;
        _edge.SourcePortId = _beforeTargetPortId;
        _edge.TargetPortId = _beforeSourcePortId;
        _edge.SourceEdgeId = _beforeTargetEdgeId;
        _edge.TargetEdgeId = _beforeSourceEdgeId;
        _edge.SourceEdgeT = _beforeTargetEdgeT;
        _edge.TargetEdgeT = _beforeSourceEdgeT;
        _edge.SourcePoint = _beforeTargetPoint is null ? null : new DiagramPoint(_beforeTargetPoint.X, _beforeTargetPoint.Y);
        _edge.TargetPoint = _beforeSourcePoint is null ? null : new DiagramPoint(_beforeSourcePoint.X, _beforeSourcePoint.Y);
        _edge.SourceConstraint = _beforeTargetConstraint?.Clone();
        _edge.TargetConstraint = _beforeSourceConstraint?.Clone();
        _edge.SourceSpacing = _beforeTargetSpacing;
        _edge.TargetSpacing = _beforeSourceSpacing;
        _edge.SourceCardinality = _beforeTargetCardinality;
        _edge.TargetCardinality = _beforeSourceCardinality;
        // StartArrow/EndArrow intentionally NOT swapped – see comment in ctor.
        _edge.LabelPositionT = 1.0 - _beforeLabelPositionT;

        _edge.Waypoints.Clear();
        foreach (var wp in _afterWaypoints)
            _edge.Waypoints.Add(wp);
    }

    public void Undo()
    {
        _edge.SourceNodeId = _beforeSourceNodeId;
        _edge.TargetNodeId = _beforeTargetNodeId;
        _edge.SourcePortId = _beforeSourcePortId;
        _edge.TargetPortId = _beforeTargetPortId;
        _edge.SourceEdgeId = _beforeSourceEdgeId;
        _edge.TargetEdgeId = _beforeTargetEdgeId;
        _edge.SourceEdgeT = _beforeSourceEdgeT;
        _edge.TargetEdgeT = _beforeTargetEdgeT;
        _edge.SourcePoint = _beforeSourcePoint is null ? null : new DiagramPoint(_beforeSourcePoint.X, _beforeSourcePoint.Y);
        _edge.TargetPoint = _beforeTargetPoint is null ? null : new DiagramPoint(_beforeTargetPoint.X, _beforeTargetPoint.Y);
        _edge.SourceConstraint = _beforeSourceConstraint?.Clone();
        _edge.TargetConstraint = _beforeTargetConstraint?.Clone();
        _edge.SourceSpacing = _beforeSourceSpacing;
        _edge.TargetSpacing = _beforeTargetSpacing;
        _edge.SourceCardinality = _beforeSourceCardinality;
        _edge.TargetCardinality = _beforeTargetCardinality;
        // StartArrow/EndArrow intentionally NOT swapped – see comment in ctor.

        _edge.Waypoints.Clear();
        foreach (var wp in _beforeWaypoints)
            _edge.Waypoints.Add(wp);
    }
}
