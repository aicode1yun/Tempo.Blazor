namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>List projection of an email template.</summary>
public sealed record EmailTemplateSummaryDto
{
    /// <summary>Gets the template identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the email subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the language code.</summary>
    public string Language { get; init; } = "cs";

    /// <summary>Gets whether the template is active.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Gets the last update timestamp.</summary>
    public DateTime? UpdatedAt { get; init; }
}
