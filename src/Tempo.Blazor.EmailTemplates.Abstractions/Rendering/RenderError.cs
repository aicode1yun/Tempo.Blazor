namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>An error or warning produced while compiling MJML to HTML.</summary>
/// <param name="Message">Human-readable description of the problem.</param>
/// <param name="Line">1-based source line, when known.</param>
/// <param name="Column">1-based source column, when known.</param>
public sealed record RenderError(string Message, int? Line = null, int? Column = null);
