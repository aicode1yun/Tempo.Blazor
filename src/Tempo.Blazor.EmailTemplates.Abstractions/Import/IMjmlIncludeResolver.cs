namespace Tempo.Blazor.EmailTemplates.Abstractions.Import;

/// <summary>Resolves the content referenced by an <c>mj-include</c> element.</summary>
public interface IMjmlIncludeResolver
{
    /// <summary>Returns the MJML/markup for the given include path, or <see langword="null"/> if unavailable.</summary>
    string? Resolve(string path);
}
