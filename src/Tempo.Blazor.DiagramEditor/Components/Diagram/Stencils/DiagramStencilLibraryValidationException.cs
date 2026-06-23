using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Exception raised when a data-driven diagram stencil library is invalid.</summary>
public sealed class DiagramStencilLibraryValidationException : InvalidOperationException
{
    /// <summary>Creates a new exception with validation errors.</summary>
    public DiagramStencilLibraryValidationException(IEnumerable<DiagramStencilLibraryValidationError> errors)
        : base("Diagram stencil library validation failed.")
    {
        Errors = errors.ToList();
    }

    /// <summary>Machine-readable validation errors.</summary>
    public IReadOnlyList<DiagramStencilLibraryValidationError> Errors { get; }
}
