using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// A sandboxed <see cref="ITemplateEngine"/> built on Scriban. Rendering is bounded by
/// <see cref="TemplateSecurityOptions"/> (loops, recursion, output size, time), <c>include</c> is
/// disabled (no template loader) and no .NET reflection is exposed.
/// </summary>
public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly TemplateSecurityOptions _options;

    /// <summary>Initializes the engine with the given sandbox options (defaults when omitted).</summary>
    public ScribanTemplateEngine(TemplateSecurityOptions? options = null)
        => _options = options ?? new TemplateSecurityOptions();

    /// <inheritdoc />
    public Result<string> Render(string template, object? model)
    {
        var parsed = Template.Parse(template);
        if (parsed.HasErrors)
            return Result<string>.Failure(string.Join("; ", parsed.Messages.Select(m => m.ToString())));

        try
        {
            var context = CreateContext();
            context.PushGlobal(ObjectToScriptObjectConverter.ToScriptObject(model));
            var output = new SandboxedScriptOutput(_options.MaxOutputLength, _options.Timeout);
            context.PushOutput(output);

            parsed.Render(context);
            return Result<string>.Success(output.ToString());
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public TemplateValidationResult Validate(string template)
    {
        var parsed = Template.Parse(template);
        if (!parsed.HasErrors) return TemplateValidationResult.Valid;

        var errors = parsed.Messages
            .Where(m => m.Type == ParserMessageType.Error)
            .Select(m => new TemplateError(m.Message, m.Span.Start.Line + 1, m.Span.Start.Column + 1))
            .ToList();
        return TemplateValidationResult.Invalid(errors);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ExtractVariables(string template)
        => TemplateVariableExtractor.Extract(template);

    private TemplateContext CreateContext()
    {
        var context = new TemplateContext
        {
            LoopLimit = _options.LoopLimit,
            RecursiveLimit = _options.RecursiveLimit,
            StrictVariables = _options.StrictVariables,
            EnableRelaxedMemberAccess = !_options.StrictVariables,
            // No TemplateLoader is set on purpose: this makes `include` fail instead of reading files.
        };
        return context;
    }
}
