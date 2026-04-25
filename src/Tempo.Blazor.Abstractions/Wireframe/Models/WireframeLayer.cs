namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Represents a layer in a wireframe page. Elements can be assigned to layers
/// to control visibility, locking, and grouping.</summary>
public sealed class WireframeLayer
{
    /// <summary>Unique layer identifier.</summary>
    public string Id { get; set; } = "l" + Guid.NewGuid().ToString("N")[..6];

    /// <summary>Display name shown in the Layers panel.</summary>
    public string Name { get; set; } = "Layer";

    /// <summary>Determines z-order / list order. Lower values render first.</summary>
    public int Order { get; set; }

    /// <summary>When false, elements on this layer are hidden on the canvas.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>When true, elements on this layer cannot be selected, moved, or edited.</summary>
    public bool IsLocked { get; set; }
}
