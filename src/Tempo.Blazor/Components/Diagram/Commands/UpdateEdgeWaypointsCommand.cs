using System;
using System.Collections.Generic;
using System.Linq;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the waypoints of a single edge and recalculates attachment points for connected edges.</summary>
public sealed class UpdateEdgeWaypointsCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly List<DiagramPoint> _oldWaypoints;
    private readonly List<DiagramPoint> _newWaypoints;
    private readonly Dictionary<string, (double? SourceT, double? TargetT, double OldX, double OldY)> _attached;

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
        _attached = new();

        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        foreach (var attached in _doc.Edges.Where(e => e.SourceEdgeId == _edgeId || e.TargetEdgeId == _edgeId))
        {
            double t = attached.SourceEdgeId == _edgeId
                ? (attached.SourceEdgeT ?? 0.5)
                : (attached.TargetEdgeT ?? 0.5);
            var pt = DiagramGeometryHelper.ComputeEdgePointAtT(_doc, edge, t);
            _attached[attached.Id] = (attached.SourceEdgeT, attached.TargetEdgeT, pt.X, pt.Y);
        }
    }

    public string Name => "Update edge path";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Waypoints = _newWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        RecalculateAttachedTs(edge);
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Waypoints = _oldWaypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        foreach (var attached in _doc.Edges.Where(e => e.SourceEdgeId == _edgeId || e.TargetEdgeId == _edgeId))
        {
            if (_attached.TryGetValue(attached.Id, out var data))
            {
                if (attached.SourceEdgeId == _edgeId) attached.SourceEdgeT = data.SourceT;
                if (attached.TargetEdgeId == _edgeId) attached.TargetEdgeT = data.TargetT;
            }
        }
    }

    private void RecalculateAttachedTs(DiagramEdge edge)
    {
        foreach (var attached in _doc.Edges.Where(e => e.SourceEdgeId == _edgeId || e.TargetEdgeId == _edgeId))
        {
            if (!_attached.TryGetValue(attached.Id, out var data)) continue;
            double newT = DiagramGeometryHelper.FindClosestT(_doc, edge, data.OldX, data.OldY);
            if (attached.SourceEdgeId == _edgeId) attached.SourceEdgeT = newT;
            if (attached.TargetEdgeId == _edgeId) attached.TargetEdgeT = newT;
        }
    }
}
