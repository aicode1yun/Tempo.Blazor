using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates an edge terminal (source or target) between node/port, point, or constraint modes.</summary>
public sealed class UpdateEdgeTerminalCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly bool _isSource;

    private readonly string? _oldNodeId;
    private readonly string? _oldPortId;
    private readonly DiagramPoint? _oldPoint;
    private readonly DiagramConnectionConstraint? _oldConstraint;

    private readonly string? _newNodeId;
    private readonly string? _newPortId;
    private readonly DiagramPoint? _newPoint;
    private readonly DiagramConnectionConstraint? _newConstraint;

    public UpdateEdgeTerminalCommand(
        DiagramDocument doc,
        string edgeId,
        bool isSource,
        string? oldNodeId,
        string? oldPortId,
        DiagramPoint? oldPoint,
        DiagramConnectionConstraint? oldConstraint,
        string? newNodeId,
        string? newPortId,
        DiagramPoint? newPoint,
        DiagramConnectionConstraint? newConstraint)
    {
        _doc = doc;
        _edgeId = edgeId;
        _isSource = isSource;
        _oldNodeId = oldNodeId;
        _oldPortId = oldPortId;
        _oldPoint = oldPoint is not null ? new DiagramPoint(oldPoint.X, oldPoint.Y) : null;
        _oldConstraint = oldConstraint?.Clone();
        _newNodeId = newNodeId;
        _newPortId = newPortId;
        _newPoint = newPoint is not null ? new DiagramPoint(newPoint.X, newPoint.Y) : null;
        _newConstraint = newConstraint?.Clone();
    }

    public string Name => _isSource ? "Reconnect source" : "Reconnect target";

    public void Execute() => Apply(_newNodeId, _newPortId, _newPoint, _newConstraint);

    public void Undo() => Apply(_oldNodeId, _oldPortId, _oldPoint, _oldConstraint);

    private void Apply(string? nodeId, string? portId, DiagramPoint? point, DiagramConnectionConstraint? constraint)
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        if (_isSource)
        {
            edge.SourceNodeId = nodeId;
            edge.SourcePortId = portId;
            edge.SourcePoint = point is not null ? new DiagramPoint(point.X, point.Y) : null;
            edge.SourceConstraint = constraint?.Clone();
        }
        else
        {
            edge.TargetNodeId = nodeId;
            edge.TargetPortId = portId;
            edge.TargetPoint = point is not null ? new DiagramPoint(point.X, point.Y) : null;
            edge.TargetConstraint = constraint?.Clone();
        }
    }
}
