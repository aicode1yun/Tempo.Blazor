namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Validation result for a data-driven diagram stencil library.</summary>
public sealed class DiagramStencilLibraryValidationResult
{
    /// <summary>Validation errors. Empty means the library is valid.</summary>
    public List<DiagramStencilLibraryValidationError> Errors { get; set; } = [];

    /// <summary>Whether the library has no validation errors.</summary>
    public bool IsValid => Errors.Count == 0;
}
