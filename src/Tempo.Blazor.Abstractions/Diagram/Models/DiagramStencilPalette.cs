namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Data-driven palette grouping related stencils inside a stencil library.</summary>
public sealed class DiagramStencilPalette
{
    /// <summary>Unique palette identifier within the library.</summary>
    public string PaletteId { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the palette display name.</summary>
    public string NameResourceKey { get; set; } = string.Empty;

    /// <summary>Display order within the library.</summary>
    public int Order { get; set; }

    /// <summary>Stencils included in this palette.</summary>
    public List<DiagramStencil> Stencils { get; set; } = [];
}
