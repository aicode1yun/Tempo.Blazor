namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

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
