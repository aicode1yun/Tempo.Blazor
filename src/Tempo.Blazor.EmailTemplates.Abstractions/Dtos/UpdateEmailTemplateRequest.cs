namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>Request to update an existing email template.</summary>
public sealed record UpdateEmailTemplateRequest
{
    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the email subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the preheader text.</summary>
    public string? Preheader { get; init; }

    /// <summary>Gets the language code.</summary>
    public string Language { get; init; } = "cs";

    /// <summary>Gets the serialized document content (JSON).</summary>
    public string ContentJson { get; init; } = string.Empty;

    /// <summary>Gets whether the template is active.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Gets the optional sample data JSON.</summary>
    public string? SampleDataJson { get; init; }
}
