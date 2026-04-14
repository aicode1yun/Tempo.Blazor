namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A logical layer that groups diagram nodes for visibility and locking control.</summary>
public sealed class DiagramLayer
{
    /// <summary>Unique layer identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display name shown in the layers panel.</summary>
    public string Name { get; set; } = "Layer";

    /// <summary>When false, nodes on this layer are hidden from the canvas.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>When true, nodes on this layer cannot be moved or deleted.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Order used for sorting layers in the UI (lower = first).</summary>
    public int Order { get; set; }
}
