namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>Validation result for stencil references used by a diagram template.</summary>
public sealed class DiagramTemplateStencilValidationResult
{
    internal DiagramTemplateStencilValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    /// <summary>Gets whether the template references only valid stencils.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Gets machine-readable validation errors.</summary>
    public IReadOnlyList<string> Errors { get; }
}
