namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Data-driven diagram stencil library loaded from JSON or another external source.</summary>
public sealed class DiagramStencilLibrary
{
    /// <summary>Unique logical stencil set identifier.</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the library display name.</summary>
    public string NameResourceKey { get; set; } = string.Empty;

    /// <summary>Whether the library should be loaded only when explicitly requested.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Palettes included in this library.</summary>
    public List<DiagramStencilPalette> Palettes { get; set; } = [];
}
