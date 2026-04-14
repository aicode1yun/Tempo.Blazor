namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A directed edge (connection) between two nodes on the canvas.</summary>
public sealed class DiagramEdge
{
    /// <summary>Unique identifier (short Guid, e.g. "a3f8c21b").</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Source node identifier.</summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>Target node identifier.</summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>Optional source port identifier.</summary>
    public string? SourcePortId { get; set; }

    /// <summary>Optional target port identifier.</summary>
    public string? TargetPortId { get; set; }

    /// <summary>Edge routing type. Supported: "straight", "orthogonal", "curved".</summary>
    public string Routing { get; set; } = "straight";

    /// <summary>Connector semantic type. (e.g. "association", "inheritance", "dependency", "composition", "aggregation").</summary>
    public string ConnectorType { get; set; } = "association";

    /// <summary>Intermediate waypoints for the edge path. Empty for straight lines.</summary>
    public List<DiagramPoint> Waypoints { get; set; } = [];

    /// <summary>Optional label rendered along the connector path.</summary>
    public string? Label { get; set; }

    /// <summary>Visual style overrides for this edge.</summary>
    public DiagramStyle Style { get; set; } = new();

    /// <summary>Reserved for future collaborative editing – who has this edge locked.</summary>
    public string? LockedBy { get; set; }
}
