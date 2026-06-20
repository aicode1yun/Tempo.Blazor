using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Updates the waypoints of a connector (used by waypoint drag).</summary>
public sealed class UpdateConnectorWaypointsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _connectorId;
    private readonly List<DiagramPoint> _before;
    private List<DiagramPoint> _after;

    public UpdateConnectorWaypointsCommand(
        WireframeDocument doc,
        string connectorId,
        List<DiagramPoint> before,
        List<DiagramPoint> after)
    {
        _doc = doc;
        _connectorId = connectorId;
        _before = before.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
        _after = after.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }

    public string Name => "Edit waypoints";

    public void Execute()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Waypoints = _after.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }

    public void Undo()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Waypoints = _before.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }

    /// <summary>Coalesces consecutive waypoint updates for the same connector.</summary>
    internal bool TryCoalesce(UpdateConnectorWaypointsCommand next)
    {
        if (next._connectorId != _connectorId) return false;
        _after = next._after.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
        return true;
    }
}
