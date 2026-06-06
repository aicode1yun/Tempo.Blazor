namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Default edge values applied when an edge stencil creates or updates a diagram edge.</summary>
public sealed class DiagramEdgeStencilDefaults
{
    /// <summary>Edge routing type. Supported values match <see cref="DiagramEdge.Routing"/>.</summary>
    public string Routing { get; set; } = "straight";

    /// <summary>Connector semantic type. Supported values match <see cref="DiagramEdge.ConnectorType"/>.</summary>
    public string ConnectorType { get; set; } = "association";

    /// <summary>Edge shape type. Supported values match <see cref="DiagramEdge.Shape"/>.</summary>
    public string Shape { get; set; } = "connector";

    /// <summary>Start arrowhead style. Supported values match <see cref="DiagramEdge.StartArrow"/>.</summary>
    public string StartArrow { get; set; } = "none";

    /// <summary>End arrowhead style. Supported values match <see cref="DiagramEdge.EndArrow"/>.</summary>
    public string EndArrow { get; set; } = "classic";

    /// <summary>Start arrowhead size in pixels.</summary>
    public double? StartArrowSize { get; set; }

    /// <summary>End arrowhead size in pixels.</summary>
    public double? EndArrowSize { get; set; }

    /// <summary>Whether the start arrowhead is filled. Null uses the renderer default.</summary>
    public bool? StartArrowFill { get; set; }

    /// <summary>Whether the end arrowhead is filled. Null uses the renderer default.</summary>
    public bool? EndArrowFill { get; set; }

    /// <summary>Whether to use rounded corners on the edge path.</summary>
    public bool Rounded { get; set; }

    /// <summary>Whether curved routing uses cubic Bezier commands.</summary>
    public bool CubicBezier { get; set; }

    /// <summary>Corner radius in pixels for rounded edges.</summary>
    public double? ArcSize { get; set; }

    /// <summary>Optional default visual style for edges created from this stencil.</summary>
    public DiagramStyle? Style { get; set; }
}
