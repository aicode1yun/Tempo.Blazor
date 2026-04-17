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

    /// <summary>Optional source edge identifier (for edge-to-edge connections).</summary>
    public string? SourceEdgeId { get; set; }

    /// <summary>Optional target edge identifier (for edge-to-edge connections).</summary>
    public string? TargetEdgeId { get; set; }

    /// <summary>Edge routing type. Supported: "straight", "orthogonal", "curved".</summary>
    public string Routing { get; set; } = "straight";

    /// <summary>Connector semantic type. (e.g. "association", "inheritance", "dependency", "composition", "aggregation").</summary>
    public string ConnectorType { get; set; } = "association";

    /// <summary>Intermediate waypoints for the edge path. Empty for straight lines.</summary>
    public List<DiagramPoint> Waypoints { get; set; } = [];

    /// <summary>Optional label rendered along the connector path.</summary>
    public string? Label { get; set; }

    /// <summary>Start arrowhead style. Supported: none, classic, block, open, oval, diamond, async.</summary>
    public string StartArrow { get; set; } = "none";

    /// <summary>End arrowhead style. Supported: none, classic, block, open, oval, diamond, async.</summary>
    public string EndArrow { get; set; } = "classic";

    /// <summary>Start arrowhead size in pixels.</summary>
    public double? StartArrowSize { get; set; }

    /// <summary>End arrowhead size in pixels.</summary>
    public double? EndArrowSize { get; set; }

    /// <summary>Whether to use rounded corners on the edge path.</summary>
    public bool Rounded { get; set; }

    /// <summary>Line jump style when edges cross. Supported: arc, gap, sharp, line.</summary>
    public string? JumpStyle { get; set; }

    /// <summary>Line jump size in pixels.</summary>
    public double? JumpSize { get; set; }

    /// <summary>Spacing between source node boundary and edge start.</summary>
    public double? SourceSpacing { get; set; }

    /// <summary>Spacing between target node boundary and edge end.</summary>
    public double? TargetSpacing { get; set; }

    /// <summary>Parameter t (0-1) along the source edge for edge-to-edge connections.</summary>
    public double? SourceEdgeT { get; set; }

    /// <summary>Parameter t (0-1) along the target edge for edge-to-edge connections.</summary>
    public double? TargetEdgeT { get; set; }

    /// <summary>Label position along the edge path as parameter t (0-1). Default 0.5.</summary>
    public double LabelPositionT { get; set; } = 0.5;

    /// <summary>Source cardinality for ER diagrams.</summary>
    public string? SourceCardinality { get; set; }

    /// <summary>Target cardinality for ER diagrams.</summary>
    public string? TargetCardinality { get; set; }

    /// <summary>Visual style overrides for this edge.</summary>
    public DiagramStyle Style { get; set; } = new();

    /// <summary>Reserved for future collaborative editing – who has this edge locked.</summary>
    public string? LockedBy { get; set; }

    /// <summary>Optional hyperlink target (external URL or page:// link).</summary>
    public string? Link { get; set; }
}
