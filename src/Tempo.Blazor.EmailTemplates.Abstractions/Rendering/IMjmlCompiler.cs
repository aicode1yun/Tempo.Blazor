namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>An error or warning produced while compiling MJML to HTML.</summary>
/// <param name="Message">Human-readable description of the problem.</param>
/// <param name="Line">1-based source line, when known.</param>
/// <param name="Column">1-based source column, when known.</param>
public sealed record RenderError(string Message, int? Line = null, int? Column = null);

/// <summary>The outcome of compiling MJML markup to HTML.</summary>
/// <param name="Html">The produced HTML (may be partial or empty when errors occurred).</param>
/// <param name="Errors">Any errors reported during compilation.</param>
public sealed record MjmlCompileResult(string Html, IReadOnlyList<RenderError> Errors)
{
    /// <summary>Gets whether compilation produced no errors.</summary>
    public bool Success => Errors.Count == 0;
}

/// <summary>Compiles MJML markup into responsive HTML.</summary>
public interface IMjmlCompiler
{
    /// <summary>
    /// Compiles the given MJML to HTML. Implementations must never throw for invalid input —
    /// parse/validation failures are returned as <see cref="RenderError"/>s.
    /// </summary>
    MjmlCompileResult Compile(string mjml);
}
