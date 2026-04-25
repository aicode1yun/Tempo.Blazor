using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Updates the routing type and optionally the waypoints of a connector.</summary>
public sealed class UpdateConnectorRoutingCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _connectorId;
    private readonly string _beforeRouting;
    private readonly List<DiagramPoint> _beforeWaypoints;
    private readonly string _afterRouting;
    private readonly List<DiagramPoint> _afterWaypoints;

    public UpdateConnectorRoutingCommand(
        WireframeDocument doc,
        string connectorId,
        string beforeRouting,
        List<DiagramPoint> beforeWaypoints,
        string afterRouting,
        List<DiagramPoint> afterWaypoints)
    {
        _doc = doc;
        _connectorId = connectorId;
        _beforeRouting = beforeRouting;
        _beforeWaypoints = beforeWaypoints.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
        _afterRouting = afterRouting;
        _afterWaypoints = afterWaypoints.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }

    public string Name => "Change routing";

    public void Execute()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Routing = _afterRouting;
        c.Waypoints = _afterWaypoints.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }

    public void Undo()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Routing = _beforeRouting;
        c.Waypoints = _beforeWaypoints.Select(w => new DiagramPoint(w.X, w.Y)).ToList();
    }
}
