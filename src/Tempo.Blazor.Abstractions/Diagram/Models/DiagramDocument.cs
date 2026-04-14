using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Root document of a diagram. Serializes to/from JSON for persistence and AI-friendly editing.</summary>
public sealed class DiagramDocument
{
    /// <summary>Schema version. Bumped on breaking changes to enable migration.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Unique document identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable title shown in the editor toolbar.</summary>
    public string Title { get; set; } = "Untitled diagram";

    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; set; } = 3000;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; set; } = 2000;

    /// <summary>All nodes placed on the canvas.</summary>
    public List<DiagramNode> Nodes { get; set; } = [];

    /// <summary>All edges (connections) between nodes.</summary>
    public List<DiagramEdge> Edges { get; set; } = [];

    /// <summary>All layers in the diagram. Nodes without a <see cref="DiagramNode.LayerId" /> belong to the default layer.</summary>
    public List<DiagramLayer> Layers { get; set; } = [];

    /// <summary>UTC timestamp of document creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
