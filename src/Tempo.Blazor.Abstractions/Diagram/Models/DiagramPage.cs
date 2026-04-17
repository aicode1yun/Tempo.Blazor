namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>
/// A single page within a multi-page diagram document.
/// Each page has its own canvas dimensions, nodes, edges, and layers.
/// </summary>
public sealed class DiagramPage
{
    /// <summary>Unique page identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display name shown in the page tab.</summary>
    public string Name { get; set; } = "Page 1";

    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; set; } = 3000;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; set; } = 2000;

    /// <summary>Predefined page size. Changing this updates <see cref="Width"/> and <see cref="Height"/>.</summary>
    public DiagramPageSize PageSize { get; set; } = DiagramPageSize.Custom;

    /// <summary>Page orientation.</summary>
    public DiagramPageOrientation PageOrientation { get; set; } = DiagramPageOrientation.Portrait;

    /// <summary>All nodes placed on this page.</summary>
    public List<DiagramNode> Nodes { get; set; } = [];

    /// <summary>All edges (connections) on this page.</summary>
    public List<DiagramEdge> Edges { get; set; } = [];

    /// <summary>All layers on this page.</summary>
    public List<DiagramLayer> Layers { get; set; } = [];

    /// <summary>Measurement unit used by the rulers.</summary>
    public MeasurementUnit RulerUnit { get; set; } = MeasurementUnit.Px;

    /// <summary>Page scale factor (1.0 = 1:1).</summary>
    public double PageScale { get; set; } = 1.0;

    /// <summary>Canvas display scale for this page (zoom applied to page content).</summary>
    public double Scale { get; set; } = 1.0;
}
