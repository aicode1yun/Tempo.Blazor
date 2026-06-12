using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Import;

/// <summary>A single import diagnostic (localization key plus optional detail and position).</summary>
/// <param name="Key">Localization key describing the diagnostic.</param>
/// <param name="Detail">Optional contextual detail (e.g. the offending element name).</param>
/// <param name="Line">1-based source line, when known.</param>
/// <param name="Column">1-based source column, when known.</param>
public sealed record ImportMessage(string Key, string? Detail = null, int? Line = null, int? Column = null);

/// <summary>
/// The outcome of importing MJML markup. <see cref="Document"/> is <see langword="null"/> only when a
/// fatal error prevented producing a model; warnings describe lossy or unsupported parts.
/// </summary>
public sealed class ImportResult
{
    /// <summary>Gets the imported document, or <see langword="null"/> on fatal error.</summary>
    public EmailTemplateDocument? Document { get; init; }

    /// <summary>Gets the non-fatal findings (lossy fallbacks, unsupported features).</summary>
    public IReadOnlyList<ImportMessage> Warnings { get; init; } = Array.Empty<ImportMessage>();

    /// <summary>Gets the fatal errors that prevented (or degraded) the import.</summary>
    public IReadOnlyList<ImportMessage> Errors { get; init; } = Array.Empty<ImportMessage>();
}

/// <summary>Localization keys for import diagnostics.</summary>
public static class ImportKeys
{
    /// <summary>Input was empty.</summary>
    public const string Empty = "import.empty";

    /// <summary>The markup could not be parsed as XML.</summary>
    public const string ParseError = "import.parse_error";

    /// <summary>The root element was not <c>mjml</c>.</summary>
    public const string NotMjml = "import.not_mjml";

    /// <summary>An unknown element was preserved as a raw block.</summary>
    public const string UnknownElement = "import.unknown_element";

    /// <summary>A body-level wrapper's sections were hoisted to the top level.</summary>
    public const string WrapperFlattened = "import.wrapper_flattened";

    /// <summary>A body- or section-level element was wrapped in a column to fit the model.</summary>
    public const string ElementWrapped = "import.element_wrapped";

    /// <summary>An <c>mj-include</c> could not be resolved.</summary>
    public const string IncludeUnresolved = "import.include_unresolved";
}

/// <summary>Resolves the content referenced by an <c>mj-include</c> element.</summary>
public interface IMjmlIncludeResolver
{
    /// <summary>Returns the MJML/markup for the given include path, or <see langword="null"/> if unavailable.</summary>
    string? Resolve(string path);
}
