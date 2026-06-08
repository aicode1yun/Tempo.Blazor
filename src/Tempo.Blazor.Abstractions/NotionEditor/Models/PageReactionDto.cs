namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Aggregated reaction metadata for a Notion page.</summary>
public sealed class PageReactionDto
{
    /// <summary>Canonical page-like reaction value.</summary>
    public const string LikeReaction = "__like";

    /// <summary>Reaction value, for example a like token or emoji.</summary>
    public string Reaction { get; set; } = string.Empty;

    /// <summary>User identifiers that applied the reaction.</summary>
    public IReadOnlyList<string> UserIds { get; set; } = [];

    /// <summary>Aggregated reaction count.</summary>
    public int Count => UserIds.Count;
}

/// <summary>Request used to toggle a page reaction.</summary>
public sealed class PageReactionToggleRequest
{
    /// <summary>User identifier that toggles the reaction.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Reaction value. Used by generic emoji reaction endpoints.</summary>
    public string Reaction { get; set; } = string.Empty;
}
