using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>
/// A single page within a <see cref="WireframeDocument"/>.
/// Each page has its own canvas dimensions, elements, and connectors.
/// </summary>
public sealed class WireframePage
{
    /// <summary>Unique identifier (short Guid, e.g. "p3f8c21b").</summary>
    public string Id { get; set; } = "p" + Guid.NewGuid().ToString("N")[..7];

    /// <summary>Human-readable page name shown in the page tab.</summary>
    public string Name { get; set; } = "Page 1";

    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; set; } = 1280;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; set; } = 800;

    /// <summary>All elements placed on this page's canvas.</summary>
    public List<WireframeElement> Elements { get; set; } = [];

    /// <summary>Connectors (arrows) between elements on this page.</summary>
    public List<WireframeConnector> Connectors { get; set; } = [];

    /// <summary>Layers on this page. Always contains at least a default layer.</summary>
    public List<WireframeLayer> Layers { get; set; } = [];

    /// <summary>Id of the layer where newly created elements are placed.</summary>
    public string? ActiveLayerId { get; set; }

    /// <summary>UTC timestamp of page creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional page-level target pack override. When null, the page inherits the document target packs.</summary>
    [JsonPropertyName("targetPacks")]
    public List<string>? TargetPackIds { get; set; }

    /// <summary>Optional page-level target theme override. When null, the page inherits the document target theme.</summary>
    public string? TargetTheme { get; set; }

    /// <summary>Ensures the page has at least a default layer.</summary>
    public void EnsureDefaultLayer()
    {
        if (Layers.Count == 0)
        {
            var layer = new WireframeLayer { Name = "Default", Order = 0 };
            Layers.Add(layer);
            ActiveLayerId = layer.Id;
        }
        else if (string.IsNullOrEmpty(ActiveLayerId))
        {
            ActiveLayerId = Layers.OrderBy(l => l.Order).First().Id;
        }
    }
}
