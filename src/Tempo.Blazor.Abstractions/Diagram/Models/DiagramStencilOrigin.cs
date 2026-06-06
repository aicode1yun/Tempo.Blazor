namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Identifies the origin of a diagram stencil definition.</summary>
public enum DiagramStencilOrigin
{
    /// <summary>No origin has been declared. Registries reject this value.</summary>
    Unspecified = 0,

    /// <summary>The stencil is an original Tempo.Blazor implementation.</summary>
    TempoOriginal = 1
}
