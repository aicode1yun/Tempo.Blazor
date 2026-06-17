using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Import;

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
