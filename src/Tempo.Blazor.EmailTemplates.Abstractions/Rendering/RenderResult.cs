namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>The fully rendered output of a template: substituted subject/preheader, HTML and text.</summary>
/// <param name="Html">The rendered HTML body.</param>
/// <param name="Subject">The subject after variable substitution.</param>
/// <param name="Preheader">The preheader after variable substitution, if any.</param>
/// <param name="TextVersion">The plain-text alternative after variable substitution.</param>
/// <param name="Errors">Errors gathered across the pipeline phases, in order.</param>
public sealed record RenderResult(
    string Html,
    string Subject,
    string? Preheader,
    string TextVersion,
    IReadOnlyList<RenderError> Errors)
{
    /// <summary>Gets whether rendering completed without errors.</summary>
    public bool Success => Errors.Count == 0;
}
