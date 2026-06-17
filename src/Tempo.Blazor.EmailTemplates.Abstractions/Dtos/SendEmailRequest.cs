namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

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
