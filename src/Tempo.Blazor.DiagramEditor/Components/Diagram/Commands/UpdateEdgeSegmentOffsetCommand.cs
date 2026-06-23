using System.Collections.Generic;
using System.Linq;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>
/// Updates edge waypoints after a segment drag. Semantically equivalent to
/// <see cref="UpdateEdgeWaypointsCommand"/> but named to reflect that the
/// change originated from dragging an orthogonal/elbow segment rather than
/// an individual waypoint.
/// </summary>
public sealed class UpdateEdgeSegmentOffsetCommand : IDiagramCommand
{
    private readonly UpdateEdgeWaypointsCommand _inner;

    public UpdateEdgeSegmentOffsetCommand(
        DiagramDocument doc,
        string edgeId,
        List<DiagramPoint> oldWaypoints,
        List<DiagramPoint> newWaypoints)
    {
        _inner = new UpdateEdgeWaypointsCommand(doc, edgeId, oldWaypoints, newWaypoints);
    }

    public string Name => "Move edge segment";

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}
