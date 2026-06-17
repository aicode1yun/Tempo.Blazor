namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>The outcome of compiling MJML markup to HTML.</summary>
/// <param name="Html">The produced HTML (may be partial or empty when errors occurred).</param>
/// <param name="Errors">Any errors reported during compilation.</param>
public sealed record MjmlCompileResult(string Html, IReadOnlyList<RenderError> Errors)
{
    /// <summary>Gets whether compilation produced no errors.</summary>
    public bool Success => Errors.Count == 0;
}
