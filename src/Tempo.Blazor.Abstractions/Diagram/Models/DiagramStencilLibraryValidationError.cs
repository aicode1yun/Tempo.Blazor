namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Single validation error for a data-driven diagram stencil library.</summary>
public sealed class DiagramStencilLibraryValidationError
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>JSON-style path to the invalid field.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional non-localized diagnostic detail for tooling; never used as UI text.</summary>
    public string? Message { get; set; }
}
