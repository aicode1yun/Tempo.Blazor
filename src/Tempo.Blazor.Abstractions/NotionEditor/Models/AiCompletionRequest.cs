using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request passed to a Notion AI completion provider.</summary>
public sealed record AiCompletionRequest
{
    /// <summary>User prompt or instruction to complete.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>Optional HTML context selected from the editor or page.</summary>
    public string? ContextHtml { get; init; }

    /// <summary>Optional Notion page identifier that scopes the request.</summary>
    public string? PageId { get; init; }

    /// <summary>Optional improvement mode associated with the request.</summary>
    public AiImproveMode? Mode { get; init; }

    /// <summary>Optional target culture for translation or localization requests.</summary>
    public string? TargetCulture { get; init; }
}
