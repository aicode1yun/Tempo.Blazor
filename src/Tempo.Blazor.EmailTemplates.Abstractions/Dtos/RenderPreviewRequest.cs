namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>Request to render a preview of a template body with optional variable data.</summary>
public sealed record RenderPreviewRequest
{
    /// <summary>Gets the serialized document content (JSON).</summary>
    public string ContentJson { get; init; } = string.Empty;

    /// <summary>Gets the optional variable data as JSON.</summary>
    public string? VariablesJson { get; init; }
}
