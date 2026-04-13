using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Root document of a wireframe. Serializes to/from JSON for AI-friendly editing.</summary>
public sealed class WireframeDocument
{
    /// <summary>Schema version. Bumped on breaking changes to enable migration.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Human-readable title shown in the editor toolbar.</summary>
    public string Title { get; set; } = "Untitled wireframe";

    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; set; } = 1280;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; set; } = 800;

    /// <summary>All elements placed on the canvas.</summary>
    public List<WireframeElement> Elements { get; set; } = [];

    /// <summary>Connectors (arrows) between elements.</summary>
    public List<WireframeConnector> Connectors { get; set; } = [];

    /// <summary>UTC timestamp of document creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
