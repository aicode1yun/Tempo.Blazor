namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>Full detail of an email template, including the serialized content.</summary>
public sealed record EmailTemplateDetailDto
{
    /// <summary>Gets the template identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the email subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the preview/preheader text.</summary>
    public string? Preheader { get; init; }

    /// <summary>Gets the language code.</summary>
    public string Language { get; init; } = "cs";

    /// <summary>Gets the serialized document content (JSON).</summary>
    public string ContentJson { get; init; } = string.Empty;

    /// <summary>Gets the variables the template requires.</summary>
    public IReadOnlyList<string> RequiredVariables { get; init; } = Array.Empty<string>();

    /// <summary>Gets the sample data JSON used for previews.</summary>
    public string? SampleDataJson { get; init; }

    /// <summary>Gets whether the template is active.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Gets the last update timestamp.</summary>
    public DateTime? UpdatedAt { get; init; }
}
