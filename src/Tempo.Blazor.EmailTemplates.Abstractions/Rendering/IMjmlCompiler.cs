namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>Compiles MJML markup into responsive HTML.</summary>
public interface IMjmlCompiler
{
    /// <summary>
    /// Compiles the given MJML to HTML. Implementations must never throw for invalid input —
    /// parse/validation failures are returned as <see cref="RenderError"/>s.
    /// </summary>
    MjmlCompileResult Compile(string mjml);
}
