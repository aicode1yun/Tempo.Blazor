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

/// <summary>Request to create a new email template.</summary>
public sealed record CreateEmailTemplateRequest
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

    /// <summary>Gets the optional sample data JSON.</summary>
    public string? SampleDataJson { get; init; }
}

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

/// <summary>Request to render a preview of a template body with optional variable data.</summary>
public sealed record RenderPreviewRequest
{
    /// <summary>Gets the serialized document content (JSON).</summary>
    public string ContentJson { get; init; } = string.Empty;

    /// <summary>Gets the optional variable data as JSON.</summary>
    public string? VariablesJson { get; init; }
}

/// <summary>A render error projected for transport.</summary>
/// <param name="Message">The error message.</param>
/// <param name="Line">Optional source line.</param>
/// <param name="Column">Optional source column.</param>
public sealed record RenderErrorDto(string Message, int? Line, int? Column);

/// <summary>The result of a preview render.</summary>
public sealed record RenderPreviewResponse
{
    /// <summary>Gets the rendered HTML.</summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>Gets the rendered plain-text version.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the substituted subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gets the substituted preheader.</summary>
    public string? Preheader { get; init; }

    /// <summary>Gets the render errors, if any.</summary>
    public IReadOnlyList<RenderErrorDto> Errors { get; init; } = Array.Empty<RenderErrorDto>();
}

/// <summary>Request to render and send a stored template to recipients.</summary>
public sealed record SendEmailRequest
{
    /// <summary>Gets the template identifier.</summary>
    public Guid TemplateId { get; init; }

    /// <summary>Gets the primary recipients.</summary>
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();

    /// <summary>Gets the carbon-copy recipients.</summary>
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();

    /// <summary>Gets the variable data as JSON.</summary>
    public string? VariablesJson { get; init; }
}
