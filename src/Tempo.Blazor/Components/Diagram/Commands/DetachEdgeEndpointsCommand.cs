using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>
/// Detaches the terminal connections of an edge while preserving its current
/// visible geometry: the current source/target screen position becomes the
/// edge's floating <see cref="DiagramEdge.SourcePoint"/> /
/// <see cref="DiagramEdge.TargetPoint"/>, and all node/port/edge/constraint
/// references are cleared. Useful for converting a node-connected edge into
/// a free line (Phase 3.8).
/// </summary>
public sealed class DetachEdgeEndpointsCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly bool _detachSource;
    private readonly bool _detachTarget;

    // Before-state snapshot for undo:
    private string? _oldSourceNodeId;
    private string? _oldSourcePortId;
    private string? _oldSourceEdgeId;
    private double? _oldSourceEdgeT;
    private DiagramConnectionConstraint? _oldSourceConstraint;
    private DiagramPoint? _oldSourcePoint;

    private string? _oldTargetNodeId;
    private string? _oldTargetPortId;
    private string? _oldTargetEdgeId;
    private double? _oldTargetEdgeT;
    private DiagramConnectionConstraint? _oldTargetConstraint;
    private DiagramPoint? _oldTargetPoint;

    public DetachEdgeEndpointsCommand(DiagramDocument doc, string edgeId, bool detachSource = true, bool detachTarget = true)
    {
        _doc = doc;
        _edgeId = edgeId;
        _detachSource = detachSource;
        _detachTarget = detachTarget;
    }

    public string Name => "Detach edge endpoints";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        _oldSourceNodeId = edge.SourceNodeId;
        _oldSourcePortId = edge.SourcePortId;
        _oldSourceEdgeId = edge.SourceEdgeId;
        _oldSourceEdgeT = edge.SourceEdgeT;
        _oldSourceConstraint = edge.SourceConstraint;
        _oldSourcePoint = edge.SourcePoint;

        _oldTargetNodeId = edge.TargetNodeId;
        _oldTargetPortId = edge.TargetPortId;
        _oldTargetEdgeId = edge.TargetEdgeId;
        _oldTargetEdgeT = edge.TargetEdgeT;
        _oldTargetConstraint = edge.TargetConstraint;
        _oldTargetPoint = edge.TargetPoint;

        // Freeze current visible terminals so the edge doesn't jump when we
        // strip the connection fields.
        var points = DiagramGeometryHelper.GetEdgePoints(_doc, edge);
        if (points.Length < 2) return;
        var src = points[0];
        var tgt = points[^1];

        if (_detachSource)
        {
            edge.SourceNodeId = null;
            edge.SourcePortId = null;
            edge.SourceEdgeId = null;
            edge.SourceEdgeT = null;
            edge.SourceConstraint = null;
            edge.SourcePoint = new DiagramPoint(src.X, src.Y);
        }

        if (_detachTarget)
        {
            edge.TargetNodeId = null;
            edge.TargetPortId = null;
            edge.TargetEdgeId = null;
            edge.TargetEdgeT = null;
            edge.TargetConstraint = null;
            edge.TargetPoint = new DiagramPoint(tgt.X, tgt.Y);
        }
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        if (_detachSource)
        {
            edge.SourceNodeId = _oldSourceNodeId;
            edge.SourcePortId = _oldSourcePortId;
            edge.SourceEdgeId = _oldSourceEdgeId;
            edge.SourceEdgeT = _oldSourceEdgeT;
            edge.SourceConstraint = _oldSourceConstraint;
            edge.SourcePoint = _oldSourcePoint;
        }

        if (_detachTarget)
        {
            edge.TargetNodeId = _oldTargetNodeId;
            edge.TargetPortId = _oldTargetPortId;
            edge.TargetEdgeId = _oldTargetEdgeId;
            edge.TargetEdgeT = _oldTargetEdgeT;
            edge.TargetConstraint = _oldTargetConstraint;
            edge.TargetPoint = _oldTargetPoint;
        }
    }
}
