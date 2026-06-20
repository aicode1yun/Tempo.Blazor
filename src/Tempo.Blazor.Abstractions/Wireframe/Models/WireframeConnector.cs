using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>A directed connector (arrow) between two elements on the canvas.</summary>
public sealed class WireframeConnector
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Id of the source element.</summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>Id of the target element.</summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>Optional label rendered along the connector path.</summary>
    public string? Label { get; set; }

    /// <summary>Routing type. Supported: "straight", "orthogonal", "curved".</summary>
    public string Routing { get; set; } = "straight";

    /// <summary>Intermediate waypoints for the edge path. Empty for auto-routed lines.</summary>
    public List<DiagramPoint> Waypoints { get; set; } = [];

    /// <summary>Start arrowhead style. Supported: none, classic, block, open, oval, diamond.</summary>
    public string StartArrow { get; set; } = "none";

    /// <summary>End arrowhead style. Supported: none, classic, block, open, oval, diamond.</summary>
    public string EndArrow { get; set; } = "classic";

    /// <summary>Stroke color (hex or named color).</summary>
    public string Stroke { get; set; } = "#94a3b8";

    /// <summary>Stroke width in pixels.</summary>
    public double StrokeWidth { get; set; } = 2;

    /// <summary>Optional SVG stroke-dasharray value (e.g. "4 2").</summary>
    public string? StrokeDasharray { get; set; }

    /// <summary>Z-order index. Higher values render on top.</summary>
    public int ZIndex { get; set; }

    /// <summary>Deep clone of this connector with a new Id.</summary>
    public WireframeConnector DeepCopy()
    {
        return new WireframeConnector
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            FromId = FromId,
            ToId = ToId,
            Label = Label,
            Routing = Routing,
            Waypoints = Waypoints.Select(w => new DiagramPoint(w.X, w.Y)).ToList(),
            StartArrow = StartArrow,
            EndArrow = EndArrow,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth,
            StrokeDasharray = StrokeDasharray,
            ZIndex = ZIndex,
        };
    }
}
