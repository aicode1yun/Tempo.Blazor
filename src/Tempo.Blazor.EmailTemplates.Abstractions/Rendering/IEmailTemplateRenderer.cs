using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

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
