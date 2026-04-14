namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A single node placed on the diagram canvas.</summary>
public sealed class DiagramNode
{
    /// <summary>Unique identifier (short Guid, e.g. "a3f8c21b").</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Stencil type identifier – must match a registered stencil definition. (e.g. "uml.class").</summary>
    public string StencilId { get; set; } = string.Empty;

    /// <summary>X position of the node's top-left corner in pixels.</summary>
    public double X { get; set; }

    /// <summary>Y position of the node's top-left corner in pixels.</summary>
    public double Y { get; set; }

    /// <summary>Width in pixels.</summary>
    public double W { get; set; } = 160;

    /// <summary>Height in pixels.</summary>
    public double H { get; set; } = 120;

    /// <summary>Rotation in degrees.</summary>
    public double Rotation { get; set; }

    /// <summary>Node data keyed by property name. (e.g. "name", "attributes", "methods").</summary>
    public Dictionary<string, object> Data { get; set; } = [];

    /// <summary>Visual style overrides for this node.</summary>
    public DiagramStyle Style { get; set; } = new();

    /// <summary>Connection ports on this node. Auto-generated from stencil if empty.</summary>
    public List<DiagramPort> Ports { get; set; } = [];

    /// <summary>Stacking order. Higher value = rendered on top.</summary>
    public int ZIndex { get; set; }

    /// <summary>Optional parent node identifier for nested nodes (containers, packages, pools).</summary>
    public string? ParentNodeId { get; set; }

    /// <summary>Optional layer identifier. When null, the node belongs to the default layer.</summary>
    public string? LayerId { get; set; }

    /// <summary>Reserved for future collaborative editing – who has this node locked.</summary>
    public string? LockedBy { get; set; }
}
