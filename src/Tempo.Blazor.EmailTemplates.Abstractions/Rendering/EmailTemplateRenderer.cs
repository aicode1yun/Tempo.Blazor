using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Templating;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>
/// Default render pipeline: Scriban substitution on subject/preheader, MJML generation + Scriban
/// substitution (resolving variables and <c>VisibleWhen</c> conditions) + Mjml.Net compilation for the
/// HTML, and a substituted plain-text version. Errors from every phase are aggregated in order.
/// </summary>
public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ITemplateEngine _engine;
    private readonly MjmlGenerator _generator;
    private readonly IMjmlCompiler _compiler;
    private readonly TextVersionGenerator _textGenerator;

    /// <summary>Initializes the renderer with its pipeline collaborators.</summary>
    public EmailTemplateRenderer(
        ITemplateEngine engine,
        MjmlGenerator generator,
        IMjmlCompiler compiler,
        TextVersionGenerator textGenerator)
    {
        _engine = engine;
        _generator = generator;
        _compiler = compiler;
        _textGenerator = textGenerator;
    }

    /// <inheritdoc />
    public Task<RenderResult> RenderAsync(EmailTemplateDocument document, object? model = null, CancellationToken cancellationToken = default)
    {
        var errors = new List<RenderError>();

        var subject = Substitute(document.Subject, model, errors);
        var preheader = document.Preheader is null ? null : Substitute(document.Preheader, model, errors);

        var mjml = _generator.Generate(document);
        var resolvedMjml = Substitute(mjml, model, errors);
        var compiled = _compiler.Compile(resolvedMjml);
        errors.AddRange(compiled.Errors);

        var textVersion = Substitute(_textGenerator.Generate(document), model, errors);

        return Task.FromResult(new RenderResult(compiled.Html, subject, preheader, textVersion, errors));
    }

    private string Substitute(string template, object? model, List<RenderError> errors)
    {
        var result = _engine.Render(template, model);
        if (result.IsSuccess) return result.Value!;
        errors.Add(new RenderError(result.Error ?? "Template rendering failed."));
        return string.Empty;
    }
}
