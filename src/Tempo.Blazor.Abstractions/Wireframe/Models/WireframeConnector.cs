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
}
