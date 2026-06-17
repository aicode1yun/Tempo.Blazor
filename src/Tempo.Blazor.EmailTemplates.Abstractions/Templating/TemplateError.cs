namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>A single template syntax error with its source position.</summary>
/// <param name="Message">Human-readable description from the parser.</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number.</param>
public sealed record TemplateError(string Message, int Line, int Column);
