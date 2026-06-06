namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A collection of related diagram stencils (e.g. all UML stencils).</summary>
public sealed class DiagramStencilSet
{
    /// <summary>Unique identifier of the set.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name displayed in the toolbox.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the stencil set display name.</summary>
    public string NameResourceKey { get; set; } = string.Empty;

    /// <summary>Stencils belonging to this set.</summary>
    public List<DiagramStencil> Stencils { get; set; } = [];
}
