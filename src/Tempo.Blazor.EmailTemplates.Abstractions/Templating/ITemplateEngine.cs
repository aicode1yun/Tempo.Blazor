namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>Renders, validates and analyzes Scriban-syntax templates within a sandbox.</summary>
public interface ITemplateEngine
{
    /// <summary>Renders a template with the given model. Never throws — failures are returned.</summary>
    Result<string> Render(string template, object? model);

    /// <summary>Validates a template's syntax without rendering it.</summary>
    TemplateValidationResult Validate(string template);

    /// <summary>Extracts the variable paths referenced by a template.</summary>
    IReadOnlyList<string> ExtractVariables(string template);
}
