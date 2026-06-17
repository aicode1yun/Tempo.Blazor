namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>The result of validating a template's syntax.</summary>
public sealed class TemplateValidationResult
{
    /// <summary>Localization key used for template syntax errors.</summary>
    public const string SyntaxErrorKey = "template.syntax_error";

    /// <summary>Gets whether the template is syntactically valid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Gets the syntax errors found (empty when valid).</summary>
    public IReadOnlyList<TemplateError> Errors { get; init; } = Array.Empty<TemplateError>();

    /// <summary>A valid result.</summary>
    public static TemplateValidationResult Valid { get; } = new();

    /// <summary>Creates an invalid result with the given errors.</summary>
    public static TemplateValidationResult Invalid(IReadOnlyList<TemplateError> errors) => new() { Errors = errors };
}
