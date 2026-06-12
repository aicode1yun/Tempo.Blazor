using Tempo.Blazor.EmailTemplates.Abstractions.Model;

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

/// <summary>Renders an <see cref="EmailTemplateDocument"/> with a data model into HTML and text.</summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the document with the given model. The pipeline substitutes Scriban variables in the
    /// subject, preheader, generated MJML (incl. visibility conditions) and text version, then compiles
    /// the MJML to HTML. Never throws — failures are collected into <see cref="RenderResult.Errors"/>.
    /// </summary>
    Task<RenderResult> RenderAsync(EmailTemplateDocument document, object? model = null, CancellationToken cancellationToken = default);
}
