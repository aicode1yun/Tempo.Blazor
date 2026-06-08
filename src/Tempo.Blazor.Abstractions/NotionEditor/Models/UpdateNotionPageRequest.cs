namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request body for updating Notion page metadata.</summary>
public sealed record UpdateNotionPageRequest(
    string Title,
    string? Description,
    string? IconEmoji,
    string? IconImageUrl,
    string? CoverImageUrl,
    double? CoverImagePositionY,
    bool IsFullWidth,
    bool IsSmallText,
    bool IsLocked,
    IReadOnlyList<string>? Labels = null);
